#include <ApplicationServices/ApplicationServices.h>
#include <IOKit/hid/IOHIDManager.h>
#include <Carbon/Carbon.h>
#include <stdio.h>
#include <dispatch/dispatch.h>
#include <string.h>

static bool postShortcut(void) {
    CGEventRef down = CGEventCreateKeyboardEvent(NULL, kVK_F18, true);
    CGEventRef up = CGEventCreateKeyboardEvent(NULL, kVK_F18, false);
    bool posted = down && up && CGPreflightPostEventAccess();
    if (posted) {
        CGEventPost(kCGSessionEventTap, down);
        CGEventPost(kCGSessionEventTap, up);
        puts("MIIIW voice key -> F18");
    } else {
        fputs("F18 could not be posted. Check Accessibility permission.\n", stderr);
    }
    fflush(stdout);
    if (down) CFRelease(down);
    if (up) CFRelease(up);
    return posted;
}

static IOHIDDeviceRef receiver;
static unsigned connection;
static unsigned voiceSession;
static bool useKeyboardMic;
static bool held;
static bool dictationShortcutSent;
static bool microphoneOpen;

static bool sendReport(uint8_t command, uint8_t value, uint8_t checksum) {
    uint8_t report[128] = {2, command, value, [16] = checksum};
    IOReturn result = IOHIDDeviceSetReport(receiver, kIOHIDReportTypeOutput,
                                            2, report, sizeof(report));
    printf("HID %02x %02x -> %08x\n", command, value, result);
    fflush(stdout);
    return result == kIOReturnSuccess;
}

static void initializeReceiver(void) {
    unsigned current = connection;
    if (!receiver || !useKeyboardMic) return;
    static const struct { uint8_t command, checksum; int milliseconds; } sequence[] = {
        {0xe1, 0xf3, 0}, {0xd1, 0x93, 500}, {0xe2, 0xf5, 980}, {0xb1, 0x53, 1000},
        {0xe1, 0xf3, 1020}, {0xd1, 0x93, 1500}, {0xe2, 0xf5, 1980}, {0xb1, 0x53, 2000},
    };
    for (size_t i = 0; i < sizeof(sequence) / sizeof(sequence[0]); ++i) {
        dispatch_after(dispatch_time(DISPATCH_TIME_NOW, sequence[i].milliseconds * NSEC_PER_MSEC),
                       dispatch_get_main_queue(), ^{
            if (receiver && useKeyboardMic && connection == current) sendReport(sequence[i].command, 0, sequence[i].checksum);
        });
    }
}

static void connectReceiver(void *context, IOReturn result, void *sender,
                            IOHIDDeviceRef device) {
    if (result != kIOReturnSuccess || receiver) return;
    receiver = (IOHIDDeviceRef)CFRetain(device);
    ++connection;
    initializeReceiver();
}

static void disconnectReceiver(void *context, IOReturn result, void *sender,
                               IOHIDDeviceRef device) {
    if (receiver != device) return;
    CFRelease(receiver);
    receiver = NULL;
    ++connection;
    ++voiceSession;
    held = microphoneOpen = dictationShortcutSent = false;
}

static void handleReport(void *context, IOReturn result, void *sender,
                         IOHIDReportType type, uint32_t reportID,
                         uint8_t *report, CFIndex length) {
    if (sender != receiver || result != kIOReturnSuccess || reportID != 1 ||
        length != 17 || report[0] != 1) return;
    if (!useKeyboardMic) {
        if (report[1] == 0xb0 && report[2] == 1) postShortcut();
        return;
    }
    if (report[1] == 0xb2) {
        printf("Microphone acknowledgement: %u\n", report[2]);
        fflush(stdout);
        return;
    }
    if (report[1] != 0xb0) return;

    if (report[2] == 1 && !held) {
        held = true;
        unsigned current = ++voiceSession;
        microphoneOpen = sendReport(0xb2, 1, 0x3d);
        dispatch_after(dispatch_time(DISPATCH_TIME_NOW, 100 * NSEC_PER_MSEC),
                       dispatch_get_main_queue(), ^{
            if (held && voiceSession == current && microphoneOpen)
                dictationShortcutSent = postShortcut();
        });
    } else if (report[2] == 0 && held) {
        held = false;
        if (dictationShortcutSent) postShortcut();
        dictationShortcutSent = false;
        unsigned current = voiceSession;
        dispatch_after(dispatch_time(DISPATCH_TIME_NOW, 100 * NSEC_PER_MSEC),
                       dispatch_get_main_queue(), ^{
            if (!held && voiceSession == current && microphoneOpen &&
                sendReport(0xb2, 0, 0x55)) microphoneOpen = false;
        });
    }
}

static IOHIDManagerRef startMonitor(void) {
    const void *keys[] = {CFSTR("VendorID"), CFSTR("ProductID"),
                         CFSTR("PrimaryUsagePage"), CFSTR("PrimaryUsage"),
                         CFSTR("Transport")};
    int numbers[] = {0x3044, 0x507c, 0xff00, 1};
    const void *values[5];
    for (int i = 0; i < 4; i++) {
        values[i] = CFNumberCreate(NULL, kCFNumberIntType, &numbers[i]);
    }
    values[4] = CFSTR("USB");
    CFDictionaryRef match = CFDictionaryCreate(NULL, keys, values, 5,
        &kCFTypeDictionaryKeyCallBacks, &kCFTypeDictionaryValueCallBacks);
    for (int i = 0; i < 4; i++) CFRelease(values[i]);

    IOHIDManagerRef manager = IOHIDManagerCreate(NULL, kIOHIDOptionsTypeNone);
    IOHIDManagerSetDeviceMatching(manager, match);
    CFRelease(match);
    IOHIDManagerRegisterDeviceMatchingCallback(manager, connectReceiver, NULL);
    IOHIDManagerRegisterDeviceRemovalCallback(manager, disconnectReceiver, NULL);
    IOHIDManagerRegisterInputReportCallback(manager, handleReport, NULL);
    IOHIDManagerScheduleWithRunLoop(manager, CFRunLoopGetCurrent(), kCFRunLoopDefaultMode);
    IOReturn result = IOHIDManagerOpen(manager, kIOHIDOptionsTypeNone);
    if (result != kIOReturnSuccess) {
        fprintf(stderr, "Unable to open MIIIW HID: 0x%08x\n", result);
        CFRelease(manager);
        return NULL;
    }
    return manager;
}

#ifndef VOICE_KEY_MENU
int main(int argc, char **argv) {
    useKeyboardMic = argc == 2 && strcmp(argv[1], "--keyboard-mic") == 0;
    if (argc != 1 && !useKeyboardMic) {
        fputs("Usage: voice-key [--keyboard-mic]\n", stderr);
        return 2;
    }
    fprintf(stderr, "F18 permission at startup: %s\n",
            CGPreflightPostEventAccess() ? "granted" : "missing");
    IOHIDManagerRef manager = startMonitor();
    if (!manager) return 1;
    CFRunLoopRun();
    CFRelease(manager);
    return 0;
}
#endif
