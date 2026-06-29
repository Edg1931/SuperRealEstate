// Native speech bridge for iOS + visionOS — SFSpeechRecognizer (STT) and
// AVSpeechSynthesizer (TTS). Paired with Assets/Scripts/ARCore/AppleSpeech.cs.
//
// Xcode requirements (add once to the generated project, or via a build
// post-processor):
//   • Link  Speech.framework  and  AVFAudio/AVFoundation.framework
//   • Info.plist:
//       NSSpeechRecognitionUsageDescription = "Used to take voice commands."
//       NSMicrophoneUsageDescription        = "Used to hear your voice commands."
//
// For visionOS, set this plugin's importer to also target visionOS (or copy it
// to Assets/Plugins/VisionOS/). The same APIs are available there.

#import <Foundation/Foundation.h>
#import <Speech/Speech.h>
#import <AVFoundation/AVFoundation.h>

typedef void (*SRECallback)(const char *);

static SRECallback s_onResult  = NULL;
static SRECallback s_onPartial = NULL;
static SRECallback s_onError   = NULL;

static SFSpeechRecognizer *s_recognizer = nil;
static SFSpeechAudioBufferRecognitionRequest *s_request = nil;
static SFSpeechRecognitionTask *s_task = nil;
static AVAudioEngine *s_engine = nil;
static AVSpeechSynthesizer *s_synth = nil;

static void sreEmit(SRECallback cb, NSString *s) {
    if (cb != NULL && s != nil) { cb([s UTF8String]); }
}

extern "C" {

void _sreSpeechSetCallbacks(SRECallback onResult, SRECallback onPartial, SRECallback onError) {
    s_onResult = onResult;
    s_onPartial = onPartial;
    s_onError = onError;
}

void _sreSpeechRequestAuth(void) {
    [SFSpeechRecognizer requestAuthorization:^(SFSpeechRecognizerAuthorizationStatus status) { (void)status; }];
    [[AVAudioSession sharedInstance] requestRecordPermission:^(BOOL granted) { (void)granted; }];
}

void _sreSpeechStop(void) {
    if (s_engine != nil && s_engine.isRunning) {
        [s_engine stop];
        [s_engine.inputNode removeTapOnBus:0];
    }
    if (s_request != nil) { [s_request endAudio]; }
    if (s_task != nil) { [s_task cancel]; s_task = nil; }
    s_request = nil;
}

void _sreSpeechStart(void) {
    @try {
        if (s_recognizer == nil) { s_recognizer = [[SFSpeechRecognizer alloc] init]; }
        if (s_engine == nil)     { s_engine = [[AVAudioEngine alloc] init]; }

        _sreSpeechStop();

        NSError *err = nil;
        AVAudioSession *session = [AVAudioSession sharedInstance];
        [session setCategory:AVAudioSessionCategoryRecord
                        mode:AVAudioSessionModeMeasurement
                     options:AVAudioSessionCategoryOptionDuckOthers
                       error:&err];
        [session setActive:YES
               withOptions:AVAudioSessionSetActiveOptionNotifyOthersOnDeactivation
                     error:&err];

        s_request = [[SFSpeechAudioBufferRecognitionRequest alloc] init];
        s_request.shouldReportPartialResults = YES;

        AVAudioInputNode *input = s_engine.inputNode;
        AVAudioFormat *fmt = [input outputFormatForBus:0];
        [input installTapOnBus:0 bufferSize:1024 format:fmt
                         block:^(AVAudioPCMBuffer *buf, AVAudioTime *when) {
            (void)when;
            if (s_request != nil) { [s_request appendAudioPCMBuffer:buf]; }
        }];

        [s_engine prepare];
        if (![s_engine startAndReturnError:&err]) {
            sreEmit(s_onError, err != nil ? err.localizedDescription : @"audio engine failed");
            return;
        }

        s_task = [s_recognizer recognitionTaskWithRequest:s_request
                                            resultHandler:^(SFSpeechRecognitionResult *result, NSError *error) {
            if (result != nil) {
                NSString *text = result.bestTranscription.formattedString;
                if (result.isFinal) { sreEmit(s_onResult, text); }
                else                { sreEmit(s_onPartial, text); }
            }
            if (error != nil) { sreEmit(s_onError, error.localizedDescription); }
        }];
    } @catch (NSException *e) {
        sreEmit(s_onError, e.reason);
    }
}

void _sreTtsSpeak(const char *text) {
    if (text == NULL) { return; }
    if (s_synth == nil) { s_synth = [[AVSpeechSynthesizer alloc] init]; }
    NSString *s = [NSString stringWithUTF8String:text];
    AVSpeechUtterance *u = [AVSpeechUtterance speechUtteranceWithString:s];
    [s_synth speakUtterance:u];
}

void _sreTtsStop(void) {
    if (s_synth != nil) { [s_synth stopSpeakingAtBoundary:AVSpeechBoundaryImmediate]; }
}

bool _sreTtsIsSpeaking(void) {
    return s_synth != nil && s_synth.isSpeaking;
}

} // extern "C"
