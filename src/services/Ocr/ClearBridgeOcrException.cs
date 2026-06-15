namespace LiveCaptionsTranslator.services.Ocr
{
    public sealed class ClearBridgeOcrException : Exception
    {
        public string ErrorCode { get; }

        public ClearBridgeOcrException(string errorCode, string message, Exception? innerException = null)
            : base(message, innerException)
        {
            ErrorCode = errorCode;
        }
    }
}
