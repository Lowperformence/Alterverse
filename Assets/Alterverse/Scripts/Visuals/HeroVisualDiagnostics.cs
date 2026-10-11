using System.Diagnostics;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Success traces are opt-in; warnings and errors remain visible in normal development.</summary>
    internal static class HeroVisualDiagnostics
    {
        [Conditional("DF_VERBOSE_HERO_DIAGNOSTICS")]
        public static void Trace(string message, Object context) => UnityEngine.Debug.Log(message, context);
    }
}
