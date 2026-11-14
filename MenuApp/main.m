#import <AppKit/AppKit.h>
#import <ServiceManagement/ServiceManagement.h>
#define VOICE_KEY_MENU
#include "../VoiceKey.c"

@interface VoiceMenu : NSObject <NSApplicationDelegate, NSMenuDelegate>
@property NSStatusItem *item;
@property NSMenuItem *status;
@property NSMenuItem *permission;
@property NSMenuItem *keyboard;
@property NSMenuItem *mac;
@property NSMenuItem *login;
@property IOHIDManagerRef monitor;
@end

@implementation VoiceMenu
- (void)applicationDidFinishLaunching:(NSNotification *)notification {
    useKeyboardMic = [[NSUserDefaults standardUserDefaults] boolForKey:@"KeyboardMic"];
    self.item = [[NSStatusBar systemStatusBar] statusItemWithLength:NSVariableStatusItemLength];
    self.item.button.image = [NSImage imageWithSystemSymbolName:@"mic" accessibilityDescription:@"米物听写"];
    NSMenu *menu = [[NSMenu alloc] init];
    menu.delegate = self;
    self.status = [menu addItemWithTitle:@"" action:nil keyEquivalent:@""];
    self.permission = [menu addItemWithTitle:@"" action:@selector(authorize:) keyEquivalent:@""];
    [menu addItem:NSMenuItem.separatorItem];
    self.mac = [menu addItemWithTitle:@"Mac 麦克风 · 短按启动" action:@selector(selectMode:) keyEquivalent:@""];
    self.keyboard = [menu addItemWithTitle:@"米物麦克风 · 按住说话" action:@selector(selectMode:) keyEquivalent:@""];
    [menu addItemWithTitle:@"打开系统听写设置…" action:@selector(openSettings:) keyEquivalent:@""];
    [menu addItemWithTitle:@"首次设置…" action:@selector(showSetup:) keyEquivalent:@""];
    if (@available(macOS 13.0, *)) {
        self.login = [menu addItemWithTitle:@"登录时启动" action:@selector(toggleLogin:) keyEquivalent:@""];
    }
    [menu addItem:NSMenuItem.separatorItem];
    [menu addItemWithTitle:@"退出米物听写" action:@selector(quit:) keyEquivalent:@"q"];
    for (NSMenuItem *entry in menu.itemArray) if (entry.action) entry.target = self;
    self.item.menu = menu;
    self.monitor = startMonitor();
    if (![[NSUserDefaults standardUserDefaults] boolForKey:@"SetupShown"]) {
        [[NSUserDefaults standardUserDefaults] setBool:YES forKey:@"SetupShown"];
        [self showSetup:nil];
    }
}

- (void)menuWillOpen:(NSMenu *)menu {
    self.status.title = receiver ? @"米物接收器已连接" : (self.monitor ? @"等待米物 USB 接收器" : @"无法访问 USB 接收器");
    self.permission.title = CGPreflightPostEventAccess() ? @"辅助功能已授权" : @"开启辅助功能权限…";
    self.keyboard.state = useKeyboardMic ? NSControlStateValueOn : NSControlStateValueOff;
    self.mac.state = useKeyboardMic ? NSControlStateValueOff : NSControlStateValueOn;
    if (@available(macOS 13.0, *)) {
        self.login.state = SMAppService.mainAppService.status == SMAppServiceStatusEnabled ? NSControlStateValueOn : NSControlStateValueOff;
    }
}

- (void)authorize:(id)sender {
    if (!CGPreflightPostEventAccess()) CGRequestPostEventAccess();
}
- (void)openSettings:(id)sender {
    [[NSWorkspace sharedWorkspace] openURL:[NSURL URLWithString:@"x-apple.systempreferences:com.apple.Keyboard-Settings.extension"]];
}
- (void)selectMode:(NSMenuItem *)sender {
    if (microphoneOpen && receiver && !sendReport(0xb2, 0, 0x55)) return;
    held = microphoneOpen = dictationShortcutSent = false;
    ++voiceSession;
    ++connection;
    useKeyboardMic = sender == self.keyboard;
    [[NSUserDefaults standardUserDefaults] setBool:useKeyboardMic forKey:@"KeyboardMic"];
    initializeReceiver();
    [self showSetup:nil];
}
- (void)showSetup:(id)sender {
    [NSApp activateIgnoringOtherApps:YES];
    NSAlert *alert = [[NSAlert alloc] init];
    alert.messageText = @"设置 macOS 听写";
    NSString *source = useKeyboardMic ? @"MIIIW Elite Receiver" : @"Mac 内置麦克风";
    NSString *usage = useKeyboardMic ? @"按住米物语音键，等听写提示出现后说话，松开结束听写并关闭键盘麦克风。" : @"短按米物语音键启动听写。";
    alert.informativeText = [NSString stringWithFormat:@"插入米物 USB 接收器并切到接收器模式。\n\n在「系统设置 → 键盘 → 听写」启用听写，麦克风来源选 %@，快捷键选「自定义」。在本应用获得辅助功能授权后，按米物语音键录入 F18。\n\n%@", source, usage];
    [alert addButtonWithTitle:@"打开键盘设置"];
    [alert addButtonWithTitle:@"关闭"];
    [alert addButtonWithTitle:@"辅助功能授权"];
    NSModalResponse response = [alert runModal];
    if (response == NSAlertFirstButtonReturn) [self openSettings:nil];
    if (response == NSAlertThirdButtonReturn) [self authorize:nil];
}
- (void)toggleLogin:(id)sender API_AVAILABLE(macos(13.0)) {
    NSError *error = nil;
    SMAppService *service = SMAppService.mainAppService;
    if (service.status == SMAppServiceStatusEnabled) [service unregisterAndReturnError:&error];
    else [service registerAndReturnError:&error];
    if (error) [[NSAlert alertWithError:error] runModal];
}
- (void)quit:(id)sender { [NSApp terminate:nil]; }
- (void)applicationWillTerminate:(NSNotification *)notification {
    if (receiver && microphoneOpen) sendReport(0xb2, 0, 0x55);
    if (self.monitor) IOHIDManagerClose(self.monitor, kIOHIDOptionsTypeNone);
}
@end

int main(void) {
    @autoreleasepool {
        NSApplication *app = NSApplication.sharedApplication;
        [app setActivationPolicy:NSApplicationActivationPolicyAccessory];
        VoiceMenu *delegate = [[VoiceMenu alloc] init];
        app.delegate = delegate;
        [app run];
    }
    return 0;
}
