using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// AUD-111: <c>StartManager.RunCommand</c>/<c>RunOptionAction</c> and
/// <c>ProgressionManager.RunProgressionAction</c> keep their buttonPressed re-entrancy guard and
/// finally cleanup, but must no longer swallow an unexpected exception thrown by the wrapped
/// action - the broad catch(Exception) each used to have is gone.
///
/// Exercised through reflection against a GameObject that is created inactive and never
/// activated, so Awake/OnEnable (which resolve scene UI, start coroutines, and touch
/// PlayerControlsProvider) never run. That is safe here because RunCommand and
/// RunProgressionAction only ever read/write buttonPressed and the last-frame guard - nothing
/// Awake sets up.
/// </summary>
public class MenuActionWrapperExceptionTests
{
    [Test]
    public void StartManager_RunCommand_PropagatesActionExceptionAndRestoresButtonPressed()
    {
        GameObject go = new GameObject("StartManager-ActionWrapperTest");
        try
        {
            go.SetActive(false);
            StartManager manager = go.AddComponent<StartManager>();

            MethodInfo runCommand = typeof(StartManager).GetMethod(
                "RunCommand", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(runCommand, "StartManager.RunCommand not found - has it been renamed?");

            Action throwingAction = () => throw new InvalidOperationException("AUD-111 test action failure");

            TargetInvocationException wrapped = Assert.Throws<TargetInvocationException>(
                () => runCommand.Invoke(manager, new object[] { throwingAction }));
            Assert.IsInstanceOf<InvalidOperationException>(
                wrapped.InnerException,
                "RunCommand must let an unexpected action exception surface, not swallow it (AUD-111).");

            FieldInfo buttonPressedField = typeof(StartManager).GetField(
                "buttonPressed", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(buttonPressedField, "StartManager.buttonPressed not found - has it been renamed?");
            Assert.IsFalse(
                (bool)buttonPressedField.GetValue(manager),
                "RunCommand's finally must restore buttonPressed even when the action throws.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void ProgressionManager_RunProgressionAction_PropagatesActionExceptionAndRestoresButtonPressed()
    {
        GameObject go = new GameObject("ProgressionManager-ActionWrapperTest");
        try
        {
            go.SetActive(false);
            ProgressionManager manager = go.AddComponent<ProgressionManager>();

            MethodInfo runProgressionAction = typeof(ProgressionManager).GetMethod(
                "RunProgressionAction", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(
                runProgressionAction,
                "ProgressionManager.RunProgressionAction not found - has it been renamed?");

            Action throwingAction = () => throw new InvalidOperationException("AUD-111 test action failure");

            TargetInvocationException wrapped = Assert.Throws<TargetInvocationException>(
                () => runProgressionAction.Invoke(manager, new object[] { throwingAction }));
            Assert.IsInstanceOf<InvalidOperationException>(
                wrapped.InnerException,
                "RunProgressionAction must let an unexpected action exception surface, not swallow it (AUD-111).");

            FieldInfo buttonPressedField = typeof(ProgressionManager).GetField(
                "buttonPressed", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(buttonPressedField, "ProgressionManager.buttonPressed not found - has it been renamed?");
            Assert.IsFalse(
                (bool)buttonPressedField.GetValue(manager),
                "RunProgressionAction's finally must restore buttonPressed even when the action throws.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
