using System;

// Minimal stand-ins for the UnityEngine types the tested game files use.
// They only need to compile and record warnings; Unity provides the real ones in the game.
namespace UnityEngine
{
    public class Object { }

    public class MonoBehaviour : Object { }

    public static class Debug
    {
        // Tests read this to confirm invalid input was reported.
        public static int WarningCount;

        public static void Log(object message) { }
        public static void Log(object message, Object context) { }
        public static void LogWarning(object message) => WarningCount++;
        public static void LogWarning(object message, Object context) => WarningCount++;
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class SerializeField : Attribute { }

    [AttributeUsage(AttributeTargets.Field)]
    public class HeaderAttribute : Attribute
    {
        public HeaderAttribute(string header) { }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class TooltipAttribute : Attribute
    {
        public TooltipAttribute(string tooltip) { }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class MinAttribute : Attribute
    {
        public MinAttribute(float min) { }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class DefaultExecutionOrder : Attribute
    {
        public DefaultExecutionOrder(int order) { }
    }
}
