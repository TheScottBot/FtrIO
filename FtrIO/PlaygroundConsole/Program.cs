using FtrIO;

Playground.TestingTrue();
Playground.TestingFalse();
Playground.TestingNoAttribute();

internal static class Playground
{
    [Toggle]
    public static void TestingTrue()
    {
        Console.WriteLine("Hello, World!");
    }

    [Toggle]
    public static void TestingFalse()
    {
        Console.WriteLine("Hello, World!");
    }
    
    public static void TestingNoAttribute()
    {
        Console.WriteLine("Hello, World!");
    }
}
