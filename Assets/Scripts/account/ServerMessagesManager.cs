using Assets.Scripts.Models;
using Assets.Scripts.restapi;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ServerMessagesManager : MonoBehaviour
{
    [SerializeField]
    List<Text> serverMessagesText;
    [SerializeField]
    List<ServerMessageModel> serverMessagesModels;
    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(LoadMessages());
    }

    private IEnumerator LoadMessages()
    {
        ApiResult<List<ServerMessageModel>> result = null;
        yield return APIHelper.GetServerMessages(value => result = value);
        serverMessagesModels = result.Success && result.Value != null
            ? result.Value
            : new List<ServerMessageModel>();
        SetUiMessages();
    }

    private void SetUiMessages()
    {
        int count = Mathf.Min(serverMessagesModels.Count, serverMessagesText.Count);
        for (int i = 0; i < count; i++)
        {
            serverMessagesText[i].text = serverMessagesModels[i].Date +"\n" + serverMessagesModels[i].Message;
        }
    }
}
