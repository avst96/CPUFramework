namespace CPUFramework
{
    public class CPUDevException : Exception
    {
        public CPUDevException(string? message, Exception? innerexception) : base(message, innerexception) { }
    }
}
