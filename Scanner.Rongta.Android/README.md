# Rongta Android SDK binding

Vendor binary: `Jars/printer_library.jar`, supplied by the user in
`22ccdc740976c5871d28a3217155532f.zip`, nested package
`PrinterExample_standard_v2.0.73_2024.11.25`.

SHA-256: `1B0EF8C33746F617A268A81F76FADDE2F929A5454B291D8CA19FFF97B9134F01`.

The vendor JAR is unmodified. The example application was not incorporated.
`Transforms/Metadata.xml` adapts covariant Java factory returns and excludes
an inaccessible internal USB-driver field from the managed binding.
Serial-port native libraries are not bundled: the application uses the SDK's
Bluetooth EDR interface, not its serial-port interface.

Build with `dotnet build Scanner.Rongta.Android/Scanner.Rongta.Android.csproj`.
Upstream binary ownership and distribution terms remain with Rongta; this
repository's license does not relicense this vendor binary.
