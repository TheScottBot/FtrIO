namespace ToggleExceptions
{
    public class ToggleDoesNotExistException : Exception
    {
        public ToggleDoesNotExistException()
        {

        }

        public ToggleDoesNotExistException(string message) : base(message)
        {

        }

        public ToggleDoesNotExistException(string message, Exception inner) : base(message, inner)
        {

        }
    }
}
