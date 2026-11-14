import Foundation
import CoreBluetooth

// BLE GATT probe for MIIIW KB Elite
// Usage: ble-probe [seconds] [--connect]

let args = CommandLine.arguments
var seconds: Double = 15
var forceConnect = false
for a in args.dropFirst() {
    if a == "--connect" { forceConnect = true }
    else if let d = Double(a) { seconds = d }
}

let serviceNames: [CBUUID: String] = [
    CBUUID(string: "1800"): "Generic Access",
    CBUUID(string: "1801"): "Generic Attribute",
    CBUUID(string: "180A"): "Device Information",
    CBUUID(string: "180F"): "Battery",
    CBUUID(string: "1812"): "HID (HOGP)",
    CBUUID(string: "1813"): "Scan Parameters",
    CBUUID(string: "1814"): "Running Speed and Cadence",
    CBUUID(string: "180D"): "Heart Rate",
    CBUUID(string: "FE59"): "Nordic DFU",
    CBUUID(string: "FE9F"): "Google Fast Pair",
    CBUUID(string: "FEE0"): "Custom FEE0",
    CBUUID(string: "FEBE"): "Custom FEBE",
    CBUUID(string: "FD6B"): "Custom FD6B",
    CBUUID(string: "FFF0"): "Vendor FFF0",
    CBUUID(string: "FFF1"): "Vendor FFF1",
    CBUUID(string: "FFF2"): "Vendor FFF2",
    CBUUID(string: "FFF3"): "Vendor FFF3",
]

let charNames: [CBUUID: String] = [
    CBUUID(string: "2A00"): "Device Name",
    CBUUID(string: "2A01"): "Appearance",
    CBUUID(string: "2A04"): "Peripheral Preferred Conn Params",
    CBUUID(string: "2A05"): "Service Changed",
    CBUUID(string: "2A19"): "Battery Level",
    CBUUID(string: "2A24"): "Model Number",
    CBUUID(string: "2A25"): "Serial Number",
    CBUUID(string: "2A26"): "Firmware Revision",
    CBUUID(string: "2A27"): "Hardware Revision",
    CBUUID(string: "2A28"): "Software Revision",
    CBUUID(string: "2A29"): "Manufacturer Name",
    CBUUID(string: "2A4A"): "HID Information",
    CBUUID(string: "2A4B"): "Report Map",
    CBUUID(string: "2A4C"): "HID Control Point",
    CBUUID(string: "2A4D"): "Report",
    CBUUID(string: "2A4E"): "Protocol Mode",
    CBUUID(string: "2A22"): "Boot Keyboard Input Report",
    CBUUID(string: "2A32"): "Boot Keyboard Output Report",
    CBUUID(string: "2902"): "Client Characteristic Configuration (CCCD)",
    CBUUID(string: "2901"): "Characteristic User Description",
]

func label(_ u: CBUUID, _ table: [CBUUID: String]) -> String {
    if let s = table[u] { return "\(u.uuidString) [\(s)]" }
    return u.uuidString
}

func describeProps(_ p: CBCharacteristicProperties) -> String {
    var out: [String] = []
    if p.contains(.broadcast) { out.append("broadcast") }
    if p.contains(.read) { out.append("read") }
    if p.contains(.writeWithoutResponse) { out.append("writeNoResp") }
    if p.contains(.write) { out.append("write") }
    if p.contains(.notify) { out.append("notify") }
    if p.contains(.indicate) { out.append("indicate") }
    if p.contains(.authenticatedSignedWrites) { out.append("signedWrite") }
    if p.contains(.extendedProperties) { out.append("extendedProps") }
    if p.contains(.notifyEncryptionRequired) { out.append("notifyEnc!") }
    if p.contains(.indicateEncryptionRequired) { out.append("indicateEnc!") }
    return out.joined(separator: "|")
}

func hex(_ d: Data?) -> String {
    guard let d = d else { return "<nil>" }
    return d.map { String(format: "%02x", $0) }.joined(separator: " ")
}

final class Probe: NSObject, CBCentralManagerDelegate, CBPeripheralDelegate {
    var central: CBCentralManager!
    var target: CBPeripheral?
    var seen: Set<UUID> = []
    var lastSeen: Date = Date()
    var didDiscoverServices = false

    func start() {
        central = CBCentralManager(delegate: self, queue: nil)
    }

    func centralManagerDidUpdateState(_ central: CBCentralManager) {
        let names: [CBManagerState: String] = [
            .poweredOff: "poweredOff", .poweredOn: "poweredOn", .resetting: "resetting",
            .unauthorized: "unauthorized (approve Bluetooth permission!)",
            .unknown: "unknown", .unsupported: "unsupported",
        ]
        print("state: \(names[central.state] ?? "?")")
        guard central.state == .poweredOn else { return }
        central.scanForPeripherals(withServices: nil,
                                   options: [CBCentralManagerScanOptionAllowDuplicatesKey: true])
        print("scanning for \(seconds)s...")
    }

    func centralManager(_ central: CBCentralManager, didDiscover peripheral: CBPeripheral,
                        advertisementData: [String: Any], rssi RSSI: NSNumber) {
        let name = advertisementData[CBAdvertisementDataLocalNameKey] as? String
            ?? peripheral.name ?? "(no name)"
        let svc = (advertisementData[CBAdvertisementDataServiceUUIDsKey] as? [CBUUID])
            ?? (advertisementData[CBAdvertisementDataSolicitedServiceUUIDsKey] as? [CBUUID]) ?? []
        let isTarget = (peripheral.name ?? "").uppercased().contains("MIIIW")
            || name.uppercased().contains("MIIIW")

        if isTarget {
            if target == nil {
                target = peripheral
                peripheral.delegate = self
                print("""

                === FOUND target: \(name)  id=\(peripheral.identifier.uuidString)
                    state=\(peripheral.state.rawValue) (0=disconnected 1=connecting 2=connected)
                    rssi=\(RSSI)  advSvcs=\(svc.map { $0.uuidString })
                """)
                if peripheral.state == .connected {
                    print("peripheral already connected system-wide -> discover services directly")
                    peripheral.discoverServices(nil)
                } else if forceConnect {
                    print("connecting...")
                    central.connect(peripheral, options: nil)
                } else {
                    print("not connected by system; re-run with --connect to attach")
                }
            }
            return
        }
        if seen.insert(peripheral.identifier).inserted {
            print(String(format: "  seen %-28@ %@ rssi %@", peripheral.identifier.uuidString,
                         (name as NSString), RSSI))
        }
    }

    func centralManager(_ central: CBCentralManager, didConnect peripheral: CBPeripheral) {
        print("connected: \(peripheral.name ?? "?")")
        peripheral.discoverServices(nil)
    }

    func centralManager(_ central: CBCentralManager, didFailToConnect peripheral: CBPeripheral,
                        error: Error?) {
        print("failed to connect: \(error?.localizedDescription ?? "?")")
    }

    func centralManager(_ central: CBCentralManager, didDisconnectPeripheral peripheral: CBPeripheral,
                        error: Error?) {
        print("disconnected: \(error?.localizedDescription ?? "no error")")
    }

    func peripheral(_ peripheral: CBPeripheral, didDiscoverServices error: Error?) {
        if let error = error {
            print("discoverServices error: \(error.localizedDescription)")
            return
        }
        let services = peripheral.services ?? []
        print("services: \(services.count)")
        for s in services {
            print("  S \(label(s.uuid, serviceNames))")
            peripheral.discoverCharacteristics(nil, for: s)
        }
    }

    func peripheral(_ peripheral: CBPeripheral, didDiscoverCharacteristicsFor service: CBService,
                    error: Error?) {
        if let error = error {
            print("  ! chars error \(service.uuid): \(error.localizedDescription)")
            return
        }
        for c in service.characteristics ?? [] {
            print("    C \(label(c.uuid, charNames))  [\(describeProps(c.properties))]")
            peripheral.discoverDescriptors(for: c)
            if c.properties.contains(.read) { peripheral.readValue(for: c) }
            if c.properties.contains(.notify) || c.properties.contains(.indicate) {
                peripheral.setNotifyValue(true, for: c)
            }
        }
    }

    func peripheral(_ peripheral: CBPeripheral, didDiscoverDescriptorsFor characteristic: CBCharacteristic,
                    error: Error?) {
        for d in characteristic.descriptors ?? [] {
            print("      D \(label(d.uuid, charNames))")
            if d.uuid == CBUUID(string: "2901") { peripheral.readValue(for: d) }
        }
    }

    func peripheral(_ peripheral: CBPeripheral, didUpdateValueFor characteristic: CBCharacteristic,
                    error: Error?) {
        if let error = error {
            print("      ! value error \(label(characteristic.uuid, charNames)): \(error.localizedDescription)")
            return
        }
        print("      V \(label(characteristic.uuid, charNames)) = \(hex(characteristic.value))")
        lastSeen = Date()
    }

    func peripheral(_ peripheral: CBPeripheral, didUpdateValueFor descriptor: CBDescriptor,
                    error: Error?) {
        print("      D-value \(descriptor.uuid.uuidString) = \(descriptor.value ?? "nil")")
    }

    func peripheral(_ peripheral: CBPeripheral, didWriteValueFor characteristic: CBCharacteristic,
                    error: Error?) {
        print("      wrote \(label(characteristic.uuid, charNames)): \(error?.localizedDescription ?? "ok")")
    }

    func peripheral(_ peripheral: CBPeripheral, didUpdateNotificationStateFor characteristic: CBCharacteristic,
                    error: Error?) {
        print("      notify \(error == nil ? "ON" : "FAILED") \(label(characteristic.uuid, charNames)) \(error?.localizedDescription ?? "")")
    }
}

let probe = Probe()
probe.start()
RunLoop.current.run(until: Date().addingTimeInterval(seconds))
print("done. target=\(probe.target?.name ?? "none") servicesDiscovered=\(probe.didDiscoverServices)")
