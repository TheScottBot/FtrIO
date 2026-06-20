namespace ToggleExceptions
{
    public class ToggleAttributeMissingException : Exception
    {
        public ToggleAttributeMissingException()
        {

        }

        public ToggleAttributeMissingException(string message) : base(message)
        {

        }

        public ToggleAttributeMissingException(string message, Exception inner) : base(message, inner)
        {

        }
    }
}
