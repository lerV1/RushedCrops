using System;
using System.Collections.Generic;

// A tiny test runner with no packages to install. The process exits with 1 if any test fails,
// which is what makes the CI job go red.
public class TestRunner
{
    private readonly List<(string name, Action body)> tests = new List<(string, Action)>();

    public void Test(string name, Action body) => tests.Add((name, body));

    public static int Main()
    {
        var run = new TestRunner();
        InventoryTests.Register(run);
        WorldClockTests.Register(run);
        return run.RunAll();
    }

    private int RunAll()
    {
        int failed = 0;
        foreach (var (name, body) in tests)
        {
            try
            {
                body();
                Console.WriteLine($"PASS {name}");
            }
            catch (Exception e)
            {
                failed++;
                Console.WriteLine($"FAIL {name}: {e.Message}");
            }
        }

        Console.WriteLine($"{tests.Count - failed} passed, {failed} failed");
        return failed == 0 ? 0 : 1;
    }
}

public static class Assert
{
    public static void True(bool condition, string message = "expected true")
    {
        if (!condition) throw new Exception(message);
    }

    public static void False(bool condition, string message = "expected false")
    {
        if (condition) throw new Exception(message);
    }

    public static void Equal(int expected, int actual, string message = null)
    {
        if (expected != actual)
        {
            throw new Exception($"{message ?? "values differ"}: expected {expected}, got {actual}");
        }
    }
}
