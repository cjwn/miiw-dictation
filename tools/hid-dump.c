#include <CoreFoundation/CoreFoundation.h>
#include <IOKit/hid/IOHIDManager.h>
#include <stdio.h>
#include <stdlib.h>

static void inputReport(void *ctx, IOReturn result, void *sender,
                        IOHIDReportType type, uint32_t reportID,
                        uint8_t *report, CFIndex length) {
    IOHIDDeviceRef dev = (IOHIDDeviceRef)sender;
    CFStringRef product = IOHIDDeviceGetProperty(dev, CFSTR("Product"));
    CFStringRef transport = IOHIDDeviceGetProperty(dev, CFSTR("Transport"));
    char p[128] = "?", t[64] = "?";
    if (product) CFStringGetCString(product, p, sizeof p, kCFStringEncodingUTF8);
    if (transport) CFStringGetCString(transport, t, sizeof t, kCFStringEncodingUTF8);
    printf("[%s] %s report %u len %ld:", t, p, reportID, (long)length);
    for (CFIndex i = 0; i < length; i++) printf(" %02x", report[i]);
    printf("\n");
    fflush(stdout);
}

int main(int argc, char **argv) {
    int vids[] = {0x3044};
    int pids[] = {0x507c};
    const void *keys[] = {CFSTR("VendorID"), CFSTR("ProductID")};
    int nums[2] = {vids[0], pids[0]};
    CFTypeRef vals[2];
    for (int i = 0; i < 2; i++) vals[i] = CFNumberCreate(NULL, kCFNumberIntType, &nums[i]);
    CFDictionaryRef match = CFDictionaryCreate(NULL, keys, vals, 2,
        &kCFTypeDictionaryKeyCallBacks, &kCFTypeDictionaryValueCallBacks);
    for (int i = 0; i < 2; i++) CFRelease(vals[i]);

    IOHIDManagerRef m = IOHIDManagerCreate(NULL, kIOHIDOptionsTypeNone);
    IOHIDManagerSetDeviceMatching(m, match);
    CFRelease(match);
    IOHIDManagerRegisterInputReportCallback(m, inputReport, NULL);
    IOHIDManagerScheduleWithRunLoop(m, CFRunLoopGetCurrent(), kCFRunLoopDefaultMode);
    IOReturn r = IOHIDManagerOpen(m, kIOHIDOptionsTypeNone);
    if (r != kIOReturnSuccess) {
        fprintf(stderr, "open failed: 0x%08x\n", r);
        return 1;
    }
    double secs = argc > 1 ? atof(argv[1]) : 30;
    fprintf(stderr, "dumping MIIIW input reports for %.0fs...\n", secs);
    CFRunLoopRunInMode(kCFRunLoopDefaultMode, secs, false);
    IOHIDManagerClose(m, kIOHIDOptionsTypeNone);
    return 0;
}
