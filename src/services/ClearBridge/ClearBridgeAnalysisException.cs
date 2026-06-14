namespace LiveCaptionsTranslator.services.ClearBridge
{
    public sealed class ClearBridgeAnalysisException : Exception
    {
        public string ErrorCode { get; }

        public ClearBridgeAnalysisException(string errorCode, string message)
            : base(message)
        {
            ErrorCode = errorCode;
        }

        public ClearBridgeAnalysisException(string errorCode, string message, Exception innerException)
            : base(message, innerException)
        {
            ErrorCode = errorCode;
        }
    }
}
