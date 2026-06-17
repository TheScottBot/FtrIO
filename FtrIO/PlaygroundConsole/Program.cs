using FtrIO;

Playground.TestingTrue();
Playground.TestingFalse();

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
}
