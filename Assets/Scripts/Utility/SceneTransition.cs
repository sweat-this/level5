using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.Utility
{
    /// <summary>
    /// Scene loads that leave a gameplay scene.
    ///
    /// Time.timeScale is global and survives scene loads, and the pause menu and the match-end
    /// flow both leave it at 0. Menu scenes have no Pause component to reconcile it, so anything
    /// there driven by scaled time - a WaitForSeconds, an Animator, a particle system - would sit
    /// frozen with no visible cause. Loading through here keeps "gameplay time is running" true
    /// for every scene that is not deliberately paused.
    /// </summary>
    public static class SceneTransition
    {
        /// <summary>Restores normal time flow without loading anything.</summary>
        public static void RestoreTimeScale()
        {
            Time.timeScale = 1f;
        }

        /// <summary>Restores normal time flow, then loads <paramref name="sceneName"/>.</summary>
        public static void LoadScene(string sceneName)
        {
            RestoreTimeScale();
            SceneManager.LoadScene(sceneName);
        }
    }
}
