using System.Collections;

namespace Level5.BackendV2.Tests
{
    /// <summary>
    /// Drives an <see cref="IEnumerator"/> coroutine to completion synchronously, recursively
    /// draining any nested <c>IEnumerator</c> it yields - the same nesting behavior Unity's real
    /// coroutine engine gives <c>yield return someOtherCoroutine();</c>, reproduced here so
    /// Backend V2 client code can be exercised in EditMode tests with no scene, no
    /// <c>MonoBehaviour</c> and no live network.
    /// </summary>
    public static class CoroutineTestRunner
    {
        public static void RunToCompletion(IEnumerator routine)
        {
            while (routine.MoveNext())
            {
                if (routine.Current is IEnumerator nested)
                {
                    RunToCompletion(nested);
                }
            }
        }
    }
}
