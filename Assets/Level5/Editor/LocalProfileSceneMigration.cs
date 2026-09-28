using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// One-time scene surgery for the V1 account/auth retirement: collapses
/// <c>level_00_account_createNew.unity</c> from a 5-field V1 registration form (email/username/
/// password/first name/last name, plus check-email/check-username preflight buttons) down to a single
/// profile-name field, and removes the retired "Login Existing" button from <c>level_00_account.unity</c>'s
/// hub. Mutates the scene directly via the Editor scene API (following the precedent already
/// established by <see cref="AccountTextMeshProMigration"/>/<c>OnlineAccountSceneBootstrap</c>) rather
/// than hand-edited YAML, so Unity's own serializer produces a valid result.
///
/// Kept as permanent re-run tooling (same convention <see cref="AccountTextMeshProMigration"/> uses for
/// its own one-off migrations) rather than deleted after use - safe to re-run since every step either
/// no-ops or throws loudly if its expected starting shape is not found.
/// </summary>
internal static class LocalProfileSceneMigration
{
    private const string CreateNewScenePath = "Assets/Scenes/level_00_account_createNew.unity";
    private const string HubScenePath = "Assets/Scenes/level_00_account.unity";

    [MenuItem("Level5/Migrate Local Profile Create Scene")]
    public static void MigrateCreateNewScene()
    {
        Scene scene = EditorSceneManager.OpenScene(CreateNewScenePath, OpenSceneMode.Single);

        GameObject verticalFields = GameObject.Find("verticalFields");
        if (verticalFields == null)
        {
            Debug.LogError("LocalProfileSceneMigration: 'verticalFields' not found in " + CreateNewScenePath);
            return;
        }

        Transform userNameField = verticalFields.transform.Find("userNameField");
        if (userNameField == null)
        {
            Debug.LogError("LocalProfileSceneMigration: 'userNameField' not found under verticalFields.");
            return;
        }

        // Remove the four retired field rows outright (email/password/first name/last name), each
        // with its own label, TMP_InputField, and (for email) check button as one subtree.
        foreach (string rowName in new[] { "emailField", "passwordField", "firstNameField", "lastNameField" })
        {
            Transform row = verticalFields.transform.Find(rowName);
            if (row != null)
            {
                Object.DestroyImmediate(row.gameObject);
            }
        }

        // The remaining row keeps its label and TMP_InputField (now the profile-name field) but loses
        // its check-username preflight button - a duplicate local profile name is now rejected
        // deterministically by DBHelper.CreateLocalProfile itself, so no separate availability check
        // is needed.
        Transform checkUserName = userNameField.Find("checkUserName");
        if (checkUserName != null)
        {
            Object.DestroyImmediate(checkUserName.gameObject);
        }

        SetText(userNameField.Find("userNameText"), "profile name");
        SetPlaceholder(userNameField.Find("UserNameInputField"), "enter profile name...");
        SetText(GameObject.Find("createUserButton")?.transform, "Create Profile");
        SetText(GameObject.Find("create")?.transform, "create a local profile: enter a profile name and click 'create profile'.");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("LocalProfileSceneMigration.MigrateCreateNewScene complete.");
    }

    [MenuItem("Level5/Migrate Account Hub Scene")]
    public static void MigrateHubScene()
    {
        Scene scene = EditorSceneManager.OpenScene(HubScenePath, OpenSceneMode.Single);

        AccountHubUiObjects hubUi = Object.FindAnyObjectByType<AccountHubUiObjects>();
        if (hubUi == null)
        {
            Debug.LogError("LocalProfileSceneMigration: AccountHubUiObjects not found in " + HubScenePath);
            return;
        }

        // Resolve the loginExisting button's GameObject by name before touching anything - Retired
        // from the hub's production navigation (spec: local-profile selection no longer detours
        // through a V1 password screen), but the underlying scene/AccountLoginUiObjects stays as an
        // editor-only template for OnlineAccountSceneBootstrap.
        Transform loginExisting = FindDescendant(hubUi.transform.root, "loginExisting");
        if (loginExisting != null)
        {
            Object.DestroyImmediate(loginExisting.gameObject);
        }

        // Retire the "create new account"/V1-registration framing on the remaining Create Local
        // Profile button and its description - it must not tell players to bring an
        // email/username/password (spec: no production copy may reference the retired form).
        Transform createNew = FindDescendant(hubUi.transform.root, "createNew");
        if (createNew != null)
        {
            SetText(createNew, "Create Local Profile");
            SetText(createNew.Find("description"), "Create a new local profile on this device.");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("LocalProfileSceneMigration.MigrateHubScene complete.");
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        if (root.name == name)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDescendant(root.GetChild(i), name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static void SetText(Transform target, string text)
    {
        if (target == null)
        {
            return;
        }

        TextMeshProUGUI tmp = target.GetComponent<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.text = text;
        }
    }

    private static void SetPlaceholder(Transform fieldTransform, string text)
    {
        if (fieldTransform == null)
        {
            return;
        }

        TMP_InputField field = fieldTransform.GetComponent<TMP_InputField>();
        if (field != null && field.placeholder is TextMeshProUGUI placeholder)
        {
            placeholder.text = text;
        }
    }
}
