namespace FtrIO
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class IoAttribute : Attribute
    {
        public string MethodName { get; set; }
        public bool Enabled { get; set; }
        public IoAttribute()
        {
            this.MethodName = string.Empty;
            this.Enabled = false;
            k
        }

    }
}