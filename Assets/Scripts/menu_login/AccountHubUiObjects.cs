using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The account hub screen (<c>level_00_account</c>): navigation to the three account sub-screens.
/// This scene has no email/username/password fields at all - see the other <c>Account*UiObjects</c>
/// types for those. Footer buttons live on <see cref="MenuFooterUiObjects"/>, not here.
/// </summary>
public class AccountHubUiObjects : MonoBehaviour
{
    [SerializeField] private Button createNewButton;
    [SerializeField] private Button loginExistingButton;
    [SerializeField] private Button loginLocalButton;

    public Button CreateNewButton => createNewButton;
    public Button LoginExistingButton => loginExistingButton;
    public Button LoginLocalButton => loginLocalButton;

    public bool Validate(List<string> missing)
    {
        int before = missing.Count;
        if (createNewButton == null) missing.Add("AccountHubUiObjects.createNewButton");
        if (loginExistingButton == null) missing.Add("AccountHubUiObjects.loginExistingButton");
        if (loginLocalButton == null) missing.Add("AccountHubUiObjects.loginLocalButton");
        return missing.Count == before;
    }
}
