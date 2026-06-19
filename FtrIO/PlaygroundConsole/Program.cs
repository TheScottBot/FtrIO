using FtrIO;
using FtrIO.Classes;

ToggleParserProvider.Configure(new ToggleParser());

Playground.TestingTrue();
Playground.TestingFalse();
Playground.TestingNoAttribute();

internal static class Playground
{
    [Toggle]
    public static void TestingTrue()
    {
        Console.WriteLine("Hello, World! TestingTrue");
    }

    [Toggle]
    public static void TestingFalse()
    {
        Console.WriteLine("Hello, World! TestingFalse");
    }
    
    public static void TestingNoAttribute()
    {
        Console.WriteLine("Hello, World! TestingNoAttribute");
    }
}
