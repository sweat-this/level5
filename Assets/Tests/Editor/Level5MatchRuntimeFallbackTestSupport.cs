using System;
using System.Reflection;

/// <summary>
/// Reaches <c>GameOptions</c>'s private <c>CaptureMatchRuntimeSnapshot</c> - the real production
/// builder <see cref="MatchRuntime"/>'s legacy fallback resolves through - so a test can install
/// exactly the same mapping production uses instead of a fake, the same way other fixtures in this
/// assembly reach a private field or method through reflection rather than widening a production
/// type's public surface just to make it testable.
/// </summary>
public static class Level5MatchRuntimeFallbackTestSupport
{
    public static Func<LegacyMatchRuntimeSnapshot> ProductionReader()
    {
        MethodInfo method = typeof(GameOptions).GetMethod(
            "CaptureMatchRuntimeSnapshot",
            BindingFlags.NonPublic | BindingFlags.Static);

        if (method == null)
        {
            throw new MissingMethodException("GameOptions.CaptureMatchRuntimeSnapshot not found - has it been renamed?");
        }

        return (Func<LegacyMatchRuntimeSnapshot>)method.CreateDelegate(typeof(Func<LegacyMatchRuntimeSnapshot>));
    }
}
