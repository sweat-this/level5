
using Assets.Scripts.database;
using Assets.Scripts.Models;
using Assets.Scripts.Utility;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Assets.Scripts.restapi
{
    public static class APIHelper
    {
        static bool apiLocked;
        private static string bearerToken;
        static int timeout = 1500;
        //private static string username;

        // -------------------------------------- HTTTP POST Highscore -------------------------------------------

        // POST highscore by scoreid by hitting api at
        // http://13.58.224.237/api/highscores/scoreid/{scoreid}
        // return true if status code == 200 ok
        // return false if status code != 200 ok
        public static IEnumerator PostHighscore(HighScoreModel score)
        {
            // note * make this async. sending the request and hitting api should do
            // this automatically.
            // put something like if(!201 created, try again) limit to 10 tries
            // check uniquescoreid not already inserted. hit that api first, then proceed

            // wait for database operations
            yield return new WaitUntil(() => !DBHelper.instance.DatabaseLocked);
            //DBHelper.instance.DatabaseLocked = true;

            // wait for api operations
            yield return new WaitUntil(() => !apiLocked);
            apiLocked = true;

            //// verify unique scoreid does NOT exist in database already
            //if (!APIHelper.ScoreIdExists(dbHighScoreModel.Scoreid))
            //{
            //serialize highscore to json for HTTP POST
            string toJson = JsonUtility.ToJson(score);
            Debug.Log(toJson);
            HttpWebResponse httpResponse = null;
            HttpStatusCode statusCode;
            try
            {
                Debug.Log("try...post single score");
                var httpWebRequest = (HttpWebRequest)WebRequest.Create(Constants.API_ADDRESS_DEV_publicApiHighScores) as HttpWebRequest;
                httpWebRequest.Timeout = timeout;
                httpWebRequest.ContentType = "application/json; charset=utf-8";
                httpWebRequest.Method = "POST";
                httpWebRequest.Headers.Add("Authorization", "Bearer " + bearerToken);
                //post
                using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                {
                    string json = toJson;
                    Debug.Log("json : " + json);
                    streamWriter.Write(json);
                    streamWriter.Flush();
                }
                // response
                httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();
                    //Debug.Log("result : " + result);
                }
            }
            // on web exception
            catch (WebException e)
            {
                httpResponse = (HttpWebResponse)e.Response;
                Debug.Log("----------------- ERROR : " + e);
                //unlock api + database
                apiLocked = false;
                DBHelper.instance.DatabaseLocked = false;
            }
            statusCode = httpResponse.StatusCode;

            // if successful
            if (httpResponse.StatusCode == HttpStatusCode.Created)
            {
                Debug.Log("----------------- HTTP POST successful : " + (int)statusCode + " " + statusCode + "  scoreid : " + score.Scoreid);
                DBHelper.instance.setGameScoreSubmitted(score.Scoreid, true);
                apiLocked = false;
                DBHelper.instance.DatabaseLocked = false;
            }
            // failed
            else
            {
                Debug.Log("----------------- HTTP POST failed : " + (int)statusCode + " " + statusCode + "  scoreid : " + score.Scoreid);
                //unlock api + database
                DBHelper.instance.setGameScoreSubmitted(score.Scoreid, false);
                apiLocked = false;
                DBHelper.instance.DatabaseLocked = false;
            }
        }

        // -------------------------------------- HTTTP POST character profile info -------------------------------------------

        // POST highscore by scoreid by hitting api at
        // http://13.58.224.237/api/highscores/scoreid/{scoreid}
        // return true if status code == 200 ok
        // return false if status code != 200 ok
        public static IEnumerator PutCharacterProfileStats(List<CharacterProfile> characters)
        {
            // note * make this async. sending the request and hitting api should do
            // this automatically.
            // put something like if(!201 created, try again) limit to 10 tries
            // check uniquescoreid not already inserted. hit that api first, then proceed

            // wait for database operations
            yield return new WaitUntil(() => !DBHelper.instance.DatabaseLocked);
            //DBHelper.instance.DatabaseLocked = true;

            // wait for api operations
            yield return new WaitUntil(() => !apiLocked);
            apiLocked = true;

            //foreach (HighScoreModel score in highscores)
            //{
            //    //serialize highscore to json for HTTP POST
            //    string toJson = JsonUtility.ToJson(score);

            //    HttpWebResponse httpResponse = null;
            //    HttpStatusCode statusCode;

            //    try
            //    {
            //        var httpWebRequest = (HttpWebRequest)WebRequest.Create(Constants.API_ADDRESS_DEV_publicApiHighScores) as HttpWebRequest;
            //        httpWebRequest.ContentType = "application/json; charset=utf-8";
            //        httpWebRequest.Method = "POST";
            //        httpWebRequest.Headers.Add("Authorization", "Bearer " + bearerToken);
            //        //post
            //        using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
            //        {
            //            string json = toJson;

            //            streamWriter.Write(json);
            //            streamWriter.Flush();
            //        }
            //        // response
            //        httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            //        using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            //        {
            //            var result = streamReader.ReadToEnd();
            //        }
            //    }
            //    // on web exception
            //    catch (WebException e)
            //    {
            //        httpResponse = (HttpWebResponse)e.Response;
            //        //unlock api + database
            //        apiLocked = false;
            //        DBHelper.instance.DatabaseLocked = false;
            //    }
            //    statusCode = httpResponse.StatusCode;
            //    // if successful
            //    if (httpResponse.StatusCode == HttpStatusCode.Created)
            //    {
            //        Debug.Log("----------------- HTTP POST successful : " + (int)statusCode + " " + statusCode);
            //        DBHelper.instance.setGameScoreSubmitted(score.Scoreid, true);
            //        apiLocked = false;
            //        DBHelper.instance.DatabaseLocked = false;
            //    }
            //    // failed
            //    else
            //    {
            //        // if conflict (scoreid already exists in database)
            //        if (httpResponse.StatusCode == HttpStatusCode.Conflict)
            //        {
            //            //Debug.Log("----------------- HTTP POST failed : scoreid already exists : " + (int)statusCode + " " + statusCode);
            //            DBHelper.instance.setGameScoreSubmitted(score.Scoreid, true);
            //            apiLocked = false;
            //            DBHelper.instance.DatabaseLocked = false;
            //        }
            //        else
            //        {
            //            //unlock api + database
            //            DBHelper.instance.setGameScoreSubmitted(score.Scoreid, false);
            //            apiLocked = false;
            //            DBHelper.instance.DatabaseLocked = false;
            //        }
            //    }
            //}
        }

        // -------------------------------------- HTTTP POST unsubmitted Highscores -------------------------------------------

        // POST highscore by scoreid by hitting api at
        // http://13.58.224.237/api/highscores/scoreid/{scoreid}
        // return true if status code == 200 ok
        // return false if status code != 200 ok
        public static void PostUnsubmittedHighscores(List<HighScoreModel> highscores)
        {


            //Debug.Log("PostUnsubmittedHighscores");
            //Debug.Log(DBHelper.instance.DatabaseLocked);
            //Debug.Log(apiLocked);
            //// wait for database operations
            ////yield return new WaitUntil(() => !DBHelper.instance.DatabaseLocked);
            ////DBHelper.instance.DatabaseLocked = true;

            //// wait for api operations
            ////yield return new WaitUntil(() => !apiLocked);
            //Debug.Log("PostUnsubmittedHighscores");
            ////foreach (HighScoreModel score in highscores)
            ////{
            ////    Debug.Log("highscores : "+ score);
            ////}
            ////bool locked = false;
            //foreach (HighScoreModel score in highscores)
            //{
            //    score.Userid = GameOptions.userid;
            //    score.UserName = GameOptions.userName;
            //    //yield return new WaitUntil(() => locked = false);
            //    //locked = true;
            //    //serialize highscore to json for HTTP POST
            //    string toJson = JsonUtility.ToJson(score);

            //    HttpWebResponse httpResponse = null;
            //    HttpStatusCode statusCode;
            //    //Debug.Log("highscores : " + score.Date);
            //    try
            //    {
            //        Debug.Log("highscores : " + score.Date);
            //        var httpWebRequest = (HttpWebRequest)WebRequest.Create(Constants.API_ADDRESS_DEV_publicApiHighScoresUnsubmitted) as HttpWebRequest;
            //        httpWebRequest.Timeout = timeout;
            //        httpWebRequest.ContentType = "application/json; charset=utf-8";
            //        httpWebRequest.Method = "POST";
            //        httpWebRequest.Headers.Add("Authorization", "Bearer " + bearerToken);
            //        //post
            //        using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
            //        {
            //            Debug.Log("post");
            //            string json = toJson;
            //            Debug.Log("json : " + json);
            //            streamWriter.Write(json);
            //            Debug.Log("streamWriter.Write(json);");
            //            streamWriter.Flush();
            //            Debug.Log("streamWriter.Flush();");
            //        }
            //        // response
            //        httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            //        Debug.Log("httpResponse : "+ httpResponse);
            //        using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            //        {
            //            var result = streamReader.ReadToEnd();
            //            Debug.Log(result);
            //        }
            //    }
            //    // on web exception
            //    catch (WebException e)
            //    {
            //        httpResponse = (HttpWebResponse)e.Response;
            //        Debug.Log("----------------- WEB EXCEPTION : " + e );
            //        //unlock api + database
            //        apiLocked = false;
            //        DBHelper.instance.DatabaseLocked = false;
            //    }
            //    statusCode = httpResponse.StatusCode;
            //    // if successful
            //    if (httpResponse.StatusCode == HttpStatusCode.Created)
            //    {
            //        Debug.Log("----------------- HTTP POST successful : " + (int)statusCode + " " + statusCode);
            //        DBHelper.instance.setGameScoreSubmitted(score.Scoreid, true);
            //        apiLocked = false;
            //        DBHelper.instance.DatabaseLocked = false;
            //    }
            //    // failed
            //    else
            //    {
            //        // if conflict (scoreid already exists in database)
            //        if (httpResponse.StatusCode == HttpStatusCode.Conflict)
            //        {
            //            Debug.Log("----------------- HTTP POST failed : scoreid already exists : " + (int)statusCode + " " + statusCode);
            //            DBHelper.instance.setGameScoreSubmitted(score.Scoreid, true);
            //            apiLocked = false;
            //            DBHelper.instance.DatabaseLocked = false;
            //        }
            //        else
            //        {
            //            //unlock api + database
            //            DBHelper.instance.setGameScoreSubmitted(score.Scoreid, false);
            //            apiLocked = false;
            //            DBHelper.instance.DatabaseLocked = false;
            //        }
            //    }
            //    //locked = false;
            //}


            Debug.Log("PostUnsubmittedHighscores");
            Debug.Log(DBHelper.instance.DatabaseLocked);
            Debug.Log(apiLocked);
            // wait for database operations
            //yield return new WaitUntil(() => !DBHelper.instance.DatabaseLocked);
            DBHelper.instance.DatabaseLocked = true;

            // wait for api operations
            //yield return new WaitUntil(() => !apiLocked);
            Debug.Log("PostUnsubmittedHighscores" + highscores.ToArray());
            //foreach (HighScoreModel score in highscores)
            //{
            //    Debug.Log("highscores : "+ score);
            //}
            //bool locked = false;
            foreach (HighScoreModel score in highscores)
            {
                Debug.Log(score.Scoreid);
                score.Userid = GameOptions.userid;
                score.UserName = GameOptions.userName;
            }
            //yield return new WaitUntil(() => locked = false);
            //locked = true;
            //serialize highscore to json for HTTP POST
            //string toJson = JsonUtility.ToJson(highscores);
            //string toJson = JsonUtility.ToJson(highscores);
            string toJson = JsonConvert.SerializeObject(highscores, Formatting.Indented);

            HttpWebResponse httpResponse = null;
            HttpStatusCode statusCode;
            Debug.Log("highscores : " + toJson);


            try
            {
                //HttpClient client = new HttpClient();
                //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                //client.DefaultRequestHeaders.Authorization =
                //new AuthenticationHeaderValue("Bearer", bearerToken);
                Debug.Log("highscores : " + toJson);
                var httpWebRequest = (HttpWebRequest)WebRequest.Create(Constants.API_ADDRESS_DEV_publicApiHighScoresUnsubmitted) as HttpWebRequest;
                httpWebRequest.Timeout = timeout;
                httpWebRequest.ContentType = "application/json; charset=utf-8";
                httpWebRequest.Method = "POST";
                httpWebRequest.Headers.Add("Authorization", "Bearer " + bearerToken);
                httpWebRequest.Accept = "application/json";
                //post
                using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                {

                    streamWriter.Write(toJson);
                    Debug.Log("streamWriter.Write(toJson);");
                    streamWriter.Flush();
                    Debug.Log("streamWriter.Flush();");
                }
                Debug.Log("post");
                string json = toJson;
                Debug.Log("json : " + toJson);
                //var content = new StringContent(toJson, Encoding.UTF8, "application/json");
                //var result = client.PostAsync(Constants.API_ADDRESS_DEV_publicApiHighScoresUnsubmitted, content).Result;
                // response
                httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                //Debug.Log("result : " + result);
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();
                    Debug.Log(result);
                }
            }
            // on web exception
            catch (WebException e)
            {
                httpResponse = (HttpWebResponse)e.Response;
                Debug.Log("----------------- WEB EXCEPTION : " + e);
                //unlock api + database
                apiLocked = false;
                DBHelper.instance.DatabaseLocked = false;
            }
            statusCode = httpResponse.StatusCode;
            // if successful
            if (httpResponse.StatusCode == HttpStatusCode.Created)
            {
                Debug.Log("----------------- HTTP POST successful : " + (int)statusCode + " " + statusCode);
                //DBHelper.instance.setGameScoreSubmitted(score.Scoreid, true);
                apiLocked = false;
                DBHelper.instance.DatabaseLocked = false;
            }
            // failed
            else
            {
                // if conflict (scoreid already exists in database)
                if (httpResponse.StatusCode == HttpStatusCode.Conflict)
                {
                    Debug.Log("----------------- HTTP POST failed : scoreid already exists : " + (int)statusCode + " " + statusCode);
                    //DBHelper.instance.setGameScoreSubmitted(score.Scoreid, true);
                    apiLocked = false;
                    DBHelper.instance.DatabaseLocked = false;
                }
                else
                {
                    //unlock api + database
                    //DBHelper.instance.setGameScoreSubmitted(score.Scoreid, false);
                    apiLocked = false;
                    DBHelper.instance.DatabaseLocked = false;
                }
            }
            //locked = false;
            //}

        }

        // -------------------------------------- HTTTP PUT Highscore -------------------------------------------

        // PUT (if doesnt exist, insert) highscore by scoreid by hitting api at
        // http://13.58.224.237/api/highscores/{scoreid}}
        // return true if status code == 200 ok
        // return false if status code != 200 ok
        public static IEnumerator PutHighscore(HighScoreModel dbHighScoreModel)
        {
            // note * make this async. sending the request and hitting api should do
            // this automatically.
            // put something like if(!201 created, try again) limit to 10 tries
            // check uniquescoreid not already inserted. hit that api first, then proceed


            yield return new WaitUntil(() => !DBHelper.instance.DatabaseLocked);

            yield return new WaitUntil(() => !apiLocked);
            apiLocked = true;

            string toJson = JsonUtility.ToJson(dbHighScoreModel);

            HttpWebResponse httpResponse = null;
            HttpStatusCode statusCode;

            try
            {
                var httpWebRequest = (HttpWebRequest)WebRequest.Create(Constants.API_ADDRESS_DEV_publicApiHighScores + dbHighScoreModel.Scoreid) as HttpWebRequest;
                httpWebRequest.Timeout = timeout;
                httpWebRequest.ContentType = "application/json; charset=utf-8";
                httpWebRequest.Method = "PUT";
                httpWebRequest.Headers.Add("Authorization", "Bearer " + bearerToken);

                using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                {
                    string json = toJson;

                    streamWriter.Write(json);
                    streamWriter.Flush();
                }

                httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();
                    Debug.Log(result);
                }
            }
            // on web exception
            catch (WebException e)
            {
                httpResponse = (HttpWebResponse)e.Response;
                Debug.Log("----------------- ERROR : " + e);
                apiLocked = false;
                DBHelper.instance.DatabaseLocked = false;
            }

            statusCode = httpResponse.StatusCode;

            // if successful
            if (httpResponse.StatusCode == HttpStatusCode.Created)
            {
                Debug.Log("----------------- HTTP PUT successful : " + (int)statusCode + " " + statusCode);
                apiLocked = false;
                DBHelper.instance.DatabaseLocked = false;
            }
            // failed
            else
            {
                Debug.Log("----------------- HTTP PUT failed : " + (int)statusCode + " " + statusCode);
                apiLocked = false;
                DBHelper.instance.DatabaseLocked = false;
            }
        }

        // -------------------------------------- HTTTP GET Highscore -------------------------------------------
        // GET highscore by scoreid by hitting api at
        // http://13.58.224.237/api/highscores/scoreid/{scoreid}
        // return true if status code == 200 ok
        // return false if status code != 200 ok
        public static List<HighScoreModel> GetHighscoreByScoreid(string scoreid)
        {

            HttpWebResponse httpResponse = null;
            HttpStatusCode statusCode;
            List<HighScoreModel> dBHighScoreModels = new List<HighScoreModel>();

            try
            {
                var httpWebRequest = (HttpWebRequest)WebRequest.Create((Constants.API_ADDRESS_DEV_publicApiHighScoresByScoreid + scoreid)) as HttpWebRequest;
                httpWebRequest.Timeout = timeout;
                httpWebRequest.ContentType = "application/json; charset=utf-8";
                httpWebRequest.Method = "GET";
                httpWebRequest.Headers.Add("Authorization", "Bearer " + bearerToken);

                httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();
                    Debug.Log(result);
                    dBHighScoreModels = (List<HighScoreModel>)JsonConvert.DeserializeObject<List<HighScoreModel>>(result);
                }

                //dBHighScoreModels = convertHttpWebResponseToDBHighscoreModelList(httpResponse);
            }
            // on web exception
            catch (WebException e)
            {
                httpResponse = (HttpWebResponse)e.Response;
                Debug.Log("----------------- ERROR : " + e);
                apiLocked = false;
            }

            statusCode = httpResponse.StatusCode;

            // if successful
            if (httpResponse.StatusCode == HttpStatusCode.OK)
            {
                Debug.Log("----------------- GetHighscoreByScoreid() successful : " + (int)statusCode + " " + statusCode);
                apiLocked = false;
            }
            // failed
            else
            {
                Debug.Log("----------------- GetHighscoreByScoreid() : " + (int)statusCode + " " + statusCode);
                apiLocked = false;
            }

            return dBHighScoreModels;
        }

        // GET highscore by scoreid by hitting api at
        // http://13.58.224.237/api/highscores/modeid/{modeid}
        // return true if status code == 200 ok
        // return false if status code != 200 ok
        public static List<StatsTableHighScoreRow> GetHighscoreByModeid(int modeid, int hardcore,
            int traffic, int enemies, int sniper, int page, int results)
        {
            HttpWebResponse httpResponse = null;
            HttpStatusCode statusCode;
            List<StatsTableHighScoreRow> highScoresList = new List<StatsTableHighScoreRow>();

            // fighting modes
            if (modeid > 19 && modeid < 23)
            {
                enemies = 1;
            }
            // build api request.
            // if no filter selected, get all scores for modeid
            // else, get specific scores
            string apiRequest = "";
            if (hardcore == 0 && traffic == 0 && enemies == 0 && sniper == 0)
            {
                apiRequest = Constants.API_ADDRESS_DEV_publicApiHighScoresByModeidInGameDisplayAll + modeid
                    + "?page=" + page
                    + "&results=" + results;
            }
            else
            {
                apiRequest = Constants.API_ADDRESS_DEV_publicApiHighScoresByModeidInGameDisplayFiltered + modeid
                    + "?hardcore=" + hardcore
                    + "&traffic=" + traffic
                    + "&enemies=" + enemies
                    + "&sniper=" + sniper
                    + "&page=" + page
                    + "&results=" + results;
            }
            //Debug.Log("apiRequest : \n" + apiRequest);
            try
            {
                var httpWebRequest = (HttpWebRequest)WebRequest.Create(apiRequest) as HttpWebRequest;
                httpWebRequest.Timeout = timeout;
                httpWebRequest.ContentType = "application/json; charset=utf-8";
                httpWebRequest.Method = "GET";
                httpWebRequest.Headers.Add("Authorization", "Bearer " + bearerToken);

                httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();
                    highScoresList = JsonConvert.DeserializeObject<List<StatsTableHighScoreRow>>(result);
                    //Debug.Log("results : \n" + result);
                }
            }
            // on web exception
            catch (WebException e)
            {
                httpResponse = (HttpWebResponse)e.Response;
                //Debug.Log("----------------- ERROR : " + e);
                apiLocked = false;
            }

            statusCode = httpResponse.StatusCode;

            // if successful
            if (httpResponse.StatusCode == HttpStatusCode.OK)
            {
                //Debug.Log("----------------- GetHighscoreByModeid() successful : " + (int)statusCode + " " + statusCode);
                apiLocked = false;
            }
            // failed
            else
            {
                //Debug.Log("----------------- GetHighscoreByModeid() : " + (int)statusCode + " " + statusCode);
                apiLocked = false;
            }

            return highScoresList;
        }

        // GET highscore by scoreid by hitting api at
        // http://13.58.224.237/api/highscores/modeid/{modeid}
        // return true if status code == 200 ok
        // return false if status code != 200 ok
        public static int GetHighscoreCountByModeid(int modeid, int hardcore,
            int traffic, int enemies, int sniper)
        {
            HttpWebResponse httpResponse = null;
            HttpStatusCode statusCode;

            //fighting modes
            if (modeid > 19 && modeid < 23)
            {
                enemies = 1;
            }

            //build api request
            string apiRequest = Constants.API_ADDRESS_DEV_publicApiHighScoresCountByModeid + modeid
                + "?hardcore=" + hardcore
                + "&traffic=" + traffic
                + "&enemies=" + enemies
                + "&sniper=" + sniper;

            //build api localhost request
            //string apiRequest = localHostHighScoresCountByModeid + modeid
            //    + "?hardcore=" + hardcore
            //    + "&traffic=" + traffic
            //    + "&enemies=" + enemies;
            //Debug.Log(apiRequest);
            int numResults = 0;
            try
            {
                var httpWebRequest = (HttpWebRequest)WebRequest.Create(apiRequest) as HttpWebRequest;
                httpWebRequest.Timeout = timeout;
                httpWebRequest.ContentType = "application/json; charset=utf-8";
                httpWebRequest.Method = "GET";
                httpWebRequest.Headers.Add("Authorization", "Bearer " + bearerToken);
                httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();

                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();
                    numResults = Convert.ToInt32(JsonConvert.DeserializeObject(result));
                    //Debug.Log("numresults : " + numResults);
                }
            }
            // on web exception
            catch (WebException e)
            {
                httpResponse = (HttpWebResponse)e.Response;
                Debug.Log("----------------- ERROR : " + e);
                apiLocked = false;
            }

            statusCode = httpResponse.StatusCode;

            // if successful
            if (httpResponse.StatusCode == HttpStatusCode.OK)
            {
                //Debug.Log("----------------- GetHighscoreCountByModeid() successful : " + (int)statusCode + " " + statusCode);
                apiLocked = false;
            }
            // failed
            else
            {
                //Debug.Log("----------------- GetHighscoreCountByModeid() : " + (int)statusCode + " " + statusCode);
                apiLocked = false;
            }

            return numResults;
        }


        // -------------------------------------- HTTTP POST User -------------------------------------------

        // POST highscore by scoreid by hitting api at
        // http://13.58.224.237/api/users/{username}
        // return true if status code == 200 ok
        // return false if status code != 200 ok
        public static IEnumerator PostUser(UserModel user)
        {
            // note * make this async. sending the request and hitting api should do
            // this automatically.
            // put something like if(!201 created, try again) limit to 10 tries
            // check uniquescoreid not already inserted. hit that api first, then proceed
            Debug.Log("...APIHelper.PostUser()");
            // wait for database operations
            yield return new WaitUntil(() => !DBHelper.instance.DatabaseLocked);

            // wait for api operations
            yield return new WaitUntil(() => !apiLocked);
            apiLocked = true;

            int userid;
            // verify unique scoreid does NOT exist in database already
            if (!APIHelper.UserNameExists(user.UserName))
            {
                //serialize highscore to json for HTTP POST
                string toJson = JsonUtility.ToJson(user);

                HttpWebResponse httpResponse = null;
                HttpStatusCode statusCode;
                try
                {
                    var httpWebRequest = (HttpWebRequest)WebRequest.Create(Constants.API_ADDRESS_DEV_publicApiUsers) as HttpWebRequest;
                    httpWebRequest.Timeout = timeout;
                    httpWebRequest.ContentType = "application/json; charset=utf-8";
                    httpWebRequest.Method = "POST";
                    //post
                    using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                    {
                        string json = toJson;
                        streamWriter.Write(json);
                        streamWriter.Flush();
                        //Debug.Log(json);
                    }
                    // response
                    httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                    using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                    {
                        var result = streamReader.ReadToEnd();
                        UserModel model = (UserModel)JsonConvert.DeserializeObject<UserModel>(result);
                        userid = model.Userid;
                        user.Userid = userid;

                        Debug.Log("userid from api : " + userid);
                        Debug.Log("userid going to db : " + user.Userid);

                    }
                }
                // on web exception
                catch (WebException e)
                {
                    httpResponse = (HttpWebResponse)e.Response;
                    Debug.Log("----------------- ERROR : " + e);
                    //unlock api + database
                    apiLocked = false;
                    DBHelper.instance.DatabaseLocked = false;

                }

                statusCode = httpResponse.StatusCode;

                // if successful
                if (httpResponse.StatusCode == HttpStatusCode.Created)
                {
                    Debug.Log("----------------- HTTP POST successful : " + (int)statusCode + " " + statusCode);
                    apiLocked = false;
                    // created on api, insert to local db
                    DBHelper.instance.InsertUser(user);
                    yield return new WaitUntil(() => !DBHelper.instance.DatabaseLocked);

                    // attempt to login newly created user
                    AccountManager account = UnityEngine.Object.FindAnyObjectByType<AccountManager>();
                    account.LoginUser(user.UserName);

                    //SceneManager.LoadScene(Constants.SCENE_NAME_level_00_loading);
                }
                // failed
                else
                {
                    Debug.Log("----------------- HTTP POST failed : " + (int)statusCode + " " + statusCode);
                    //unlock api + database
                    apiLocked = false;
                    DBHelper.instance.DatabaseLocked = false;
                }
            }
            else
            {
                Debug.Log(" scoreid already exists : " + user.UserName);
                apiLocked = false;
            }
        }

        // -------------------------------------- HTTTP GET User -------------------------------------------
        // GET highscore by scoreid by hitting api at
        // http://13.58.224.237/api/users/username/{username}
        // return true if status code == 200 ok
        // return false if status code != 200 ok
        public static bool UserExists(string username)
        {

            HttpWebResponse httpResponse = null;
            HttpStatusCode statusCode;
            //List<DBHighScoreModel> dBHighScoreModels = new List<DBHighScoreModel>();

            try
            {
                var httpWebRequest = (HttpWebRequest)WebRequest.Create((Constants.API_ADDRESS_DEV_publicApiUsersByUserName + username)) as HttpWebRequest;
                httpWebRequest.Timeout = timeout;
                httpWebRequest.ContentType = "application/json; charset=utf-8";
                httpWebRequest.Method = "GET";

                httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();

                //dBHighScoreModels = new List<DBHighScoreModel>();
                //dBHighScoreModels = convertHttpWebResponseToDBHighscoreModelList(httpResponse);
            }
            // on web exception
            catch (WebException e)
            {
                httpResponse = (HttpWebResponse)e.Response;
                Debug.Log("----------------- ERROR : " + e);
                apiLocked = false;
            }

            statusCode = httpResponse.StatusCode;

            // if successful
            if (httpResponse.StatusCode == HttpStatusCode.OK)
            {
                Debug.Log("----------------- GetHighscoreByScoreid() successful : " + (int)statusCode + " " + statusCode);
                apiLocked = false;
                return true;
            }
            // failed
            else
            {
                Debug.Log("----------------- GetHighscoreByScoreid() : " + (int)statusCode + " " + statusCode);
                apiLocked = false;
                return false;
            }
        }

        // --------------------------------------Utility functions -------------------------------------------

        // send HttpWebResponse to function to be serialized into List of DBHighScore objects
        public static List<HighScoreModel> convertHttpWebResponseToDBHighscoreModelList(HttpWebResponse httpWebResponse)
        {
            Debug.Log("convertHttpWebResponseToDBHighscoreModelList(HttpWebResponse httpWebResponse)");
            List<HighScoreModel> dBHighScoreModels = new List<HighScoreModel>();

            using (var streamReader = new StreamReader(httpWebResponse.GetResponseStream()))
            {
                var result = streamReader.ReadToEnd();
                Debug.Log("results 1 : \n" + result);
                dBHighScoreModels = (List<HighScoreModel>)JsonConvert.DeserializeObject<List<HighScoreModel>>(result);
                Debug.Log("results 2 : \n" + result);
            }

            return dBHighScoreModels;
        }

        // send HttpWebResponse to function to be serialized into a single DBHighScore object
        public static HighScoreModel ConvertHttpWebResponseToDBHighscoreModel(HttpWebResponse httpWebResponse)
        {

            HighScoreModel dBHighScoreModels = new HighScoreModel();
            using (var streamReader = new StreamReader(httpWebResponse.GetResponseStream()))
            {
                var result = streamReader.ReadToEnd();
                dBHighScoreModels = (HighScoreModel)JsonConvert.DeserializeObject<HighScoreModel>(result);
            }

            return dBHighScoreModels;
        }

        // check if scoreid exists by hitting api at
        // http://13.58.224.237/api/highscores/scoreid/{scoreid}
        // return true if status code == 200 ok
        // return false if status code != 200 ok
        public static bool ScoreIdExists(string scoreid)
        {

            bool exists = false;
            HttpWebResponse httpResponse = null;
            HttpStatusCode statusCode;

            //Debug.Log("scoredidexists uri :" + (Constants.API_ADDRESS_DEV_publicApiHighScoresByScoreid + scoreid));

            try
            {
                var httpWebRequest = (HttpWebRequest)WebRequest.Create((Constants.API_ADDRESS_DEV_publicApiHighScoresByScoreid + scoreid)) as HttpWebRequest;
                httpWebRequest.Timeout = timeout;
                httpWebRequest.ContentType = "application/json; charset=utf-8";
                httpWebRequest.Method = "GET";

                httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            }
            // on web exception
            catch (WebException e)
            {
                httpResponse = (HttpWebResponse)e.Response;
                //Debug.Log("----------------- ERROR : " + e);
            }

            statusCode = httpResponse.StatusCode;

            // if successful
            if (httpResponse.StatusCode == HttpStatusCode.OK)
            {
                //Debug.Log("----------------- scoreid found : " + (int)statusCode + " " + statusCode);
                exists = true;
            }
            // failed
            else
            {
                //Debug.Log("----------------- scoreid not found : " + (int)statusCode + " " + statusCode);
                exists = false;
            }

            return exists;
        }

        // check if username exists by hitting api at
        // http://13.58.224.237/api/users/username/{username}found
        // return true if status code == 200 ok
        // return false if status code != 200 ok
        public static bool UserNameExists(string username)
        {
            Debug.Log("username exists");
            bool exists = false;
            HttpWebResponse httpResponse = null;
            //HttpStatusCode statusCode;
            var httpWebRequest = (HttpWebRequest)WebRequest.Create(Constants.API_ADDRESS_DEV_publicApiUsersByUserName + username) as HttpWebRequest;
            httpWebRequest.Timeout = timeout;
            try
            {
                using (HttpWebResponse response = (HttpWebResponse)httpWebRequest.GetResponse())
                {
                    httpWebRequest.ContentType = "application/json; charset=utf-8";
                    httpWebRequest.Method = "GET";
                    //httpWebRequest.Headers.Add("Authorization", bearerToken);
                    httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                }
            }
            // on web exception
            catch (WebException e)
            {
                httpResponse = (HttpWebResponse)e.Response;
                Debug.Log("----------------- ERROR : " + e);
            }

            //statusCode = httpResponse.StatusCode;

            // if successful
            if (httpResponse.StatusCode == HttpStatusCode.OK)
            {
                //Debug.Log("----------------- username found : " + (int)statusCode + " " + statusCode);
                exists = true;
            }
            // failed
            else
            {
                //Debug.Log("----------------- username not found : " + (int)statusCode + " " + statusCode);
                exists = false;
            }

            return exists;
        }

        // check if username email by hitting api at
        // http://13.58.224.237/api/users/username/{username}
        // return true if status code == 200 ok
        // return false if status code != 200 ok
        public static bool EmailExists(string email)
        {
            bool exists = false;
            HttpWebResponse httpResponse = null;
            HttpStatusCode statusCode;
            try
            {
                var httpWebRequest = (HttpWebRequest)WebRequest.Create((Constants.API_ADDRESS_DEV_publicApiUsersByEmail + email)) as HttpWebRequest;
                httpWebRequest.Timeout = timeout;
                httpWebRequest.ContentType = "application/json; charset=utf-8";
                httpWebRequest.Method = "GET";
                httpWebRequest.Headers.Add("Authorization", bearerToken);
                httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            }
            // on web exception
            catch (WebException e)
            {
                httpResponse = (HttpWebResponse)e.Response;
                Debug.Log("----------------- ERROR : " + e);
            }

            statusCode = httpResponse.StatusCode;

            // if successful
            if (httpResponse.StatusCode == HttpStatusCode.OK)
            {
                Debug.Log("----------------- username found : " + (int)statusCode + " " + statusCode);
                exists = true;
            }
            // failed
            else
            {
                Debug.Log("----------------- username not found : " + (int)statusCode + " " + statusCode);
                exists = false;
            }

            return exists;
        }

        // check if username email by hitting api at
        // http://13.58.224.237/api/users/username/{username}
        // return true if status code == 200 ok
        // return false if status code != 200 ok
        public static UserModel GetUserByUserName(string username)
        {
            apiLocked = true;
            UserModel user = new UserModel();

            HttpWebResponse httpResponse = null;
            HttpStatusCode statusCode;
            try
            {
                var httpWebRequest = (HttpWebRequest)WebRequest.Create((Constants.API_ADDRESS_DEV_publicApiUsersByUserName + username)) as HttpWebRequest;
                httpWebRequest.Timeout = timeout;
                httpWebRequest.ContentType = "application/json; charset=utf-8";
                httpWebRequest.Method = "GET";
                httpWebRequest.Headers.Add("Authorization", bearerToken);
                httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();

                // response
                httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();
                    Debug.Log(result);
                    user = (UserModel)JsonConvert.DeserializeObject<UserModel>(result);
                }
            }
            // on web exception
            catch (WebException e)
            {
                httpResponse = (HttpWebResponse)e.Response;
                Debug.Log("----------------- ERROR : " + e);
                apiLocked = false;
            }

            statusCode = httpResponse.StatusCode;

            // if successful
            if (httpResponse.StatusCode == HttpStatusCode.OK)
            {
                //Debug.Log("----------------- username found : " + (int)statusCode + " " + statusCode);
                apiLocked = false;
            }
            // failed
            else
            {
                Debug.Log("----------------- username not found : " + (int)statusCode + " " + statusCode);
                apiLocked = false;
            }

            return user;
        }
        // -------------------------------------- HTTTP POST User report -------------------------------------------

        // POST user report
        // http://13.58.224.237/api/userreports
        // return true if status code == 200 ok
        // return false if status code != 200 ok
        public static IEnumerator PostReport(UserReportModel userReport, InputField inputField)
        {
            yield return new WaitUntil(() => !apiLocked);
            apiLocked = true;

            //Debug.Log("PostReport");
            if (DBHelper.instance != null)
            {
                yield return new WaitUntil(() => !DBHelper.instance.DatabaseLocked);
            }

            if (!String.IsNullOrEmpty(GameOptions.userName))
            {
                userReport.UserId = GameOptions.userid;
                userReport.UserName = GameOptions.userName;

            }
            else
            {
                userReport.UserId = 999;
                userReport.UserName = "not logged in";
            }
            userReport.Os = SystemInfo.operatingSystem;
            userReport.Device = SystemInfo.deviceModel;
            userReport.DeviceName = SystemInfo.deviceModel;
            userReport.Version = Application.version;
            userReport.IpAddress = UtilityFunctions.GetExternalIpAdress();
            //serialize highscore to json for HTTP POST
            string toJson = JsonUtility.ToJson(userReport);
            HttpWebResponse httpResponse = null;
            HttpStatusCode statusCode;
            try
            {
                var httpWebRequest = (HttpWebRequest)WebRequest.Create(Constants.API_ADDRESS_DEV_publicUserReport) as HttpWebRequest;
                httpWebRequest.Timeout = timeout;
                httpWebRequest.ContentType = "application/json; charset=utf-8";
                httpWebRequest.Method = "POST";

                //post
                using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                {
                    string json = toJson;
                    streamWriter.Write(json);
                    streamWriter.Flush();
                }
                // response
                httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();
                    bearerToken = result;
                }
            }
            // on web exception
            catch (WebException e)
            {
                httpResponse = (HttpWebResponse)e.Response;
                Debug.Log("----------------- ERROR : " + e);
                inputField.text = "ERROR : " + e;
                apiLocked = false;
            }

            statusCode = httpResponse.StatusCode;

            // if successful
            if (httpResponse.StatusCode == HttpStatusCode.Created)
            {
                Debug.Log("----------------- HTTP POST successful : " + (int)statusCode + " " + statusCode);
                inputField.text = "HTTP POST successful : " + (int)statusCode + " " + statusCode;
                apiLocked = false;
            }
            // failed
            else
            {
                Debug.Log("----------------- HTTP POST failed : " + (int)statusCode + " " + statusCode);
                inputField.text = "HTTP POST failed : " + (int)statusCode + " " + statusCode;
                apiLocked = false;
            }
            yield return new WaitUntil(() => !apiLocked);
        }

        // -------------------------------------- HTTTP POST Token -------------------------------------------

        // POST Token by User model to get token
        // http://13.58.224.237/api/token
        // return true if status code == 200 ok
        // return false if status code != 200 ok
        public static IEnumerator PostToken(UserModel user)
        {
            // note * make this async. sending the request and hitting api should do
            // this automatically.
            // put something like if(!201 created, try again) limit to 10 tries
            // check uniquescoreid not already inserted. hit that api first, then proceed
            // wait for database operations
            yield return new WaitUntil(() => !DBHelper.instance.DatabaseLocked);

            //serialize highscore to json for HTTP POST
            string toJson = JsonUtility.ToJson(user);
            HttpWebResponse httpResponse = null;
            HttpStatusCode statusCode;
            try
            {
                var httpWebRequest = (HttpWebRequest)WebRequest.Create(Constants.API_ADDRESS_DEV_publicApiToken) as HttpWebRequest;
                httpWebRequest.Timeout = timeout;
                httpWebRequest.ContentType = "application/json; charset=utf-8";
                httpWebRequest.Method = "POST";

                //post
                using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                {
                    string json = toJson;
                    streamWriter.Write(json);
                    streamWriter.Flush();
                }
                // response
                httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();
                    bearerToken = result;
                }
            }
            // on web exception
            catch (WebException e)
            {
                httpResponse = (HttpWebResponse)e.Response;
                Debug.Log("----------------- ERROR : " + e);
                //unlock api + database
                apiLocked = false;
                DBHelper.instance.DatabaseLocked = false;
            }
            if (httpResponse != null)
            {
                statusCode = httpResponse.StatusCode;

                // if successful
                if (httpResponse.StatusCode == HttpStatusCode.OK)
                {
                    //Debug.Log("----------------- HTTP POST successful : " + (int)statusCode + " " + statusCode);
                    GameOptions.userName = user.UserName;
                    GameOptions.userid = user.Userid;
                    GameOptions.bearerToken = APIHelper.BearerToken;

                    apiLocked = false;
                    DBHelper.instance.DatabaseLocked = false;
                }
                // failed
                else
                {
                    //Debug.Log("----------------- HTTP POST failed : " + (int)statusCode + " " + statusCode);
                    //unlock api + database
                    GameOptions.userName = user.UserName;
                    GameOptions.userid = user.Userid;

                    apiLocked = false;
                    DBHelper.instance.DatabaseLocked = false;
                }


                yield return new WaitUntil(() => !apiLocked);
                yield return new WaitUntil(() => !DBHelper.instance.DatabaseLocked);
            }
            else
            {
                apiLocked = false;
                DBHelper.instance.DatabaseLocked = false;
            }
            //Debug.Log(APIHelper.bearerToken);
            //if (httpResponse.StatusCode == HttpStatusCode.OK)
            //{
            //    SceneManager.LoadScene(Constants.SCENE_NAME_level_00_loading);
            //}
            SceneManager.LoadScene(Constants.SCENE_NAME_level_00_loading);
        }

        //------------------------------------- GET Application  ----------------------------------------
        // GET highscore by scoreid by hitting api at
        // http://13.58.224.237/api/highscores/modeid/{modeid}
        // return true if status code == 200 ok
        // return false if status code != 200 ok
        public static string GetLatestBuildVersion()
        {
            apiLocked = true;
            HttpWebResponse httpResponse = null;
            HttpStatusCode statusCode;

            string currentVersion = "";
            //build api request
            string apiRequest = Constants.API_ADDRESS_DEV_publicApplicationVersionCurrent;


            try
            {
                var httpWebRequest = (HttpWebRequest)WebRequest.Create(apiRequest) as HttpWebRequest;
                httpWebRequest.Timeout = timeout;
                httpWebRequest.ContentType = "application/json; charset=utf-8";
                httpWebRequest.Method = "GET";
                httpWebRequest.Headers.Add("Authorization", "Bearer " + bearerToken);
                httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();

                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();
                    //Debug.Log("result : " + result);
                    //currentVersion = Convert.ToString(JsonConvert.DeserializeObject(result));
                    currentVersion = result;
                }
            }
            // on web exception
            catch (WebException e)
            {
                httpResponse = (HttpWebResponse)e.Response;
                Debug.Log("----------------- ERROR : " + e);
                apiLocked = false;
                return "server down";
            }

            statusCode = httpResponse.StatusCode;

            // if successful
            if (httpResponse.StatusCode == HttpStatusCode.OK)
            {
                apiLocked = false;
            }
            // failed
            else
            {
                apiLocked = false;
            }
            //Debug.Log("api : latest build : " + currentVersion);
            return currentVersion;
        }

        //------------------------------------- GET Application  ----------------------------------------
        // GET highscore by scoreid by hitting api at
        // http://13.58.224.237/api/highscores/modeid/{modeid}
        // return true if status code == 200 ok
        // return false if status code != 200 ok
        public static List<ServerMessageModel> GetServerMessages()
        {
            apiLocked = true;
            HttpWebResponse httpResponse = null;
            HttpStatusCode statusCode;

            List<ServerMessageModel> messages = new List<ServerMessageModel>();
            //build api request
            string apiRequest = Constants.API_ADDRESS_DEV_publicServerMessages;

            //int numResults = 0;
            try
            {
                var httpWebRequest = (HttpWebRequest)WebRequest.Create(apiRequest) as HttpWebRequest;
                httpWebRequest.Timeout = timeout;
                httpWebRequest.ContentType = "application/json; charset=utf-8";
                httpWebRequest.Method = "GET";
                //httpWebRequest.Headers.Add("Authorization", "Bearer " + bearerToken);
                httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();

                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();
                    //Debug.Log("result : " + result);
                    messages = (List<ServerMessageModel>)JsonConvert.DeserializeObject<List<ServerMessageModel>>(result);
                }
            }
            // on web exception
            catch (WebException e)
            {
                httpResponse = (HttpWebResponse)e.Response;
                Debug.Log("----------------- ERROR : " + e);
                apiLocked = false;
            }
            //Debug.Log(httpResponse.StatusCode);
            statusCode = httpResponse.StatusCode;

            // if successful
            if (httpResponse.StatusCode == HttpStatusCode.OK)
            {
                //Debug.Log("----------------- GetHighscoreCountByModeid() successful : " + (int)statusCode + " " + statusCode);
                apiLocked = false;
            }
            // failed
            else
            {
                //Debug.Log("----------------- GetHighscoreCountByModeid() : " + (int)statusCode + " " + statusCode);
                apiLocked = false;
            }
            //Debug.Log("api : latest build : " + currentVersion);
            //Debug.Log("messages : " + messages);
            return messages;
        }

        public static string BearerToken { get => bearerToken; }
        public static bool ApiLocked { get => apiLocked; set => apiLocked = value; }
    }
}
