using UnityEngine;

namespace Assets.Scripts.Utility
{
    /// <summary>
    /// Lookups for scene objects a manager depends on by name.
    ///
    /// The pattern this replaces - `GameObject.Find(name).GetComponent&lt;T&gt;()` repeated a dozen
    /// times in Awake/Start - throws on the first missing object and leaves the manager half
    /// initialized with its static instance already published, and the exception never names
    /// which object was missing. These log the name and let the caller decide what to do.
    ///
    /// Level5ProjectValidator checks these names at build time so a rename fails the build
    /// rather than the play session.
    /// </summary>
    public static class SceneObjects
    {
        /// <summary>
        /// Finds an active scene object by name and returns its <typeparamref name="T"/>.
        /// Returns null and logs which name failed when the object or component is missing.
        /// </summary>
        public static T Find<T>(string objectName, Object context = null) where T : Component
        {
            GameObject found = GameObject.Find(objectName);
            if (found == null)
            {
                Debug.LogError("Scene object '" + objectName + "' was not found.", context);
                return null;
            }

            T component = found.GetComponent<T>();
            if (component == null)
            {
                Debug.LogError(
                    "Scene object '" + objectName + "' has no " + typeof(T).Name + " component.",
                    context);
            }

            return component;
        }

        /// <summary>
        /// Same as <see cref="Find{T}"/>, but also records the name in <paramref name="missing"/>
        /// so a caller can report every missing dependency at once instead of one per run.
        /// </summary>
        public static T Find<T>(string objectName, System.Collections.Generic.List<string> missing, Object context = null)
            where T : Component
        {
            T component = Find<T>(objectName, context);
            if (component == null && missing != null)
            {
                missing.Add(objectName);
            }

            return component;
        }

        /// <summary>Finds an active scene object by name, logging the name if it is missing.</summary>
        public static GameObject Find(string objectName, Object context = null)
        {
            GameObject found = GameObject.Find(objectName);
            if (found == null)
            {
                Debug.LogError("Scene object '" + objectName + "' was not found.", context);
            }

            return found;
        }
    }
}
