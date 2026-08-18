namespace recroll
{
    internal sealed class recroll_result
    {
        public bool Success { get; private set; }
        public string Message { get; private set; }
        public uint NativeCode { get; private set; }

        public static recroll_result Ok(string message) =>
            new recroll_result { Success = true, Message = message, NativeCode = 0 };

        public static recroll_result Fail(string message, uint nativeCode = 0) =>
            new recroll_result { Success = false, Message = message, NativeCode = nativeCode };
    }
}
