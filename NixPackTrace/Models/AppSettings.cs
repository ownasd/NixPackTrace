namespace NixPackTrace.Models
{
    public class AppSettings
    {
        public int    BoxSize          { get; set; } = 5;
        public string ProductName      { get; set; } = "Product ABC";
        public bool   RequireLongQr    { get; set; } = true;
        public bool   RequireTestingQr { get; set; } = true;
        
        public int    MacIdMinLength        { get; set; } = 0;
        public string MacIdRequiredText     { get; set; } = "";
        public int    LongQrMinLength       { get; set; } = 11;
        public string LongQrRequiredText    { get; set; } = "";
        public int    TestingQrMinLength    { get; set; } = 0;
        public string TestingQrRequiredText { get; set; } = "ok";

        public string FirebaseUrl      { get; set; } = "https://nix-traceability-default-rtdb.firebaseio.com/";
        public string PrinterName      { get; set; } = "";
        public string StationName      { get; set; } = "Packing Station";
        public bool   DarkMode         { get; set; } = false;
        public string AdminPassword    { get; set; } = "1234";
    }
}
