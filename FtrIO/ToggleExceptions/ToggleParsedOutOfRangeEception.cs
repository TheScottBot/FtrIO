namespace ToggleExceptions
{
    public class ToggleParsedOutOfRangeException : Exception
    {
        public ToggleParsedOutOfRangeException()
        {

        }

        public ToggleParsedOutOfRangeException(string message) : base (message)
        {

        }

        public ToggleParsedOutOfRangeException(string message, Exception inner) :base(message, inner)
        {

        }
    }
}