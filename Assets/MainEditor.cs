#if false // NOT USED
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using System.Linq;
using System;
using UnityEditor;
using UnityEngine.SceneManagement;
using static SaveSystem;
public class SaveSeralize<T>
{
#region seralized
    [Serializable]
    public struct Vector2Serializer
    {
        public float x;
        public float y;

        public static Vector2Serializer zero { get { return new Vector2Serializer(0, 0); } }
        public Vector2Serializer(float X, float Y)
        {
            x = X;
            y = Y;
        }
        public Vector2Serializer(Vector2 v3)
        {
            x = v3.x;
            y = v3.y;
        }
        public Vector2 Get()
        {
            return new Vector3(x, y);
        }
    }
    [Serializable]
    public struct Vector3Serializer
    {
        public float x;
        public float y;
        public float z;

        public static Vector3Serializer zero { get { return new Vector3Serializer(0, 0, 0); } }
        public Vector3Serializer(float X, float Y, float Z)
        {
            x = X;
            y = Y;
            z = Z;
        }
        public Vector3Serializer(Vector3 v3)
        {
            x = v3.x;
            y = v3.y;
            z = v3.z;
        }
        public Vector3 Get()
        {
            return new Vector3(x, y, z);
        }
    }
    [Serializable]
    public struct QuaternionSerializer
    {
        public float x;
        public float y;
        public float z;
        public float w;

        public static QuaternionSerializer zero { get { return new QuaternionSerializer(0, 0, 0, 0); } }
        public QuaternionSerializer(float X, float Y, float Z, float W)
        {
            x = X;
            y = Y;
            z = Z;
            w = W;
        }
        public QuaternionSerializer(Quaternion v4)
        {
            x = v4.x;
            y = v4.y;
            z = v4.z;
            w = v4.w;
        }
        public Quaternion Get()
        {
            return new Quaternion(x, y, z, w);
        }
    }
#endregion

    public interface ISaveInterface
    {
        void SaveData(T data); // Save data in seralized form
        T GetData(T data); // Get data in non seralized form
    }



    [System.Serializable]
    public class GameObjectSaveData : ISaveInterface
    {
        public Vector3Serializer pos = Vector3Serializer.zero;
        public QuaternionSerializer rot = QuaternionSerializer.zero;
        public string name = "";
        public bool active = true;

        public T GetData(T _data)
        {
            GameObject data = _data as GameObject;
            data.SetActive(active);
            data.name = name;
            data.transform.rotation = rot.Get();
            data.transform.position = pos.Get();
            return (T)Convert.ChangeType(data, typeof(T));
        }

        public void SaveData(T _g)
        {
            GameObject g = _g as GameObject;
            name = g.name;
            pos = new Vector3Serializer(g.transform.position);
            rot = new QuaternionSerializer(g.transform.rotation);
            active = g.activeSelf;
        }
    }

    [System.Serializable]
    public class Rigidbody2DSaveData : ISaveInterface
    {
        public Vector2Serializer pos = Vector2Serializer.zero;
        public Vector2Serializer vel = Vector2Serializer.zero;
        public float rot = 0;
        public float inertia = 0;

        public T GetData(T _data)
        {
            Rigidbody2D data = _data as Rigidbody2D;
            data.position = pos.Get();
            if (!data.isKinematic && !(data.bodyType == RigidbodyType2D.Static)) {
                data.velocity = vel.Get();
                data.inertia = inertia;
            }
            data.rotation = rot;
            return (T)Convert.ChangeType(data, typeof(T));
        }

        public void SaveData(T _data)
        {
            Rigidbody2D data = _data as Rigidbody2D;
            inertia = data.inertia;
            rot = data.rotation;
            pos = new Vector2Serializer(data.position);
            vel = new Vector2Serializer(data.velocity);
        }
    }

}


public class SaveSystem<T>
{
    public static bool IsEnabled { get { return SaveSystem.isEnabled; } }

    public static void Save(T save, GameObject gameObject, string extraName = "")
    {
        if (!IsEnabled) { SaveSystem.ClearData(); return; } // IF NOT ENABLED, DONT WASTE THE TIME TO SAVE

        if (!Directory.Exists(BasePath)) {
            Directory.CreateDirectory(BasePath); // MAKE SURE THAT THAT FOLDER EXISTS
        }

        string path = GetPath(gameObject, typeof(T), extraName); // GET UNIQUE SAVE PATH
        BinaryFormatter binaryFormatter = new BinaryFormatter();
        FileStream stream = new FileStream(path, FileMode.Create);

        binaryFormatter.Serialize(stream, save);
        stream.Close();
    }
    public static T Get(T save, GameObject gameObject, string extraName = "")
    {

        if (!IsEnabled) return save;

        string path = GetPath(gameObject, typeof(T), extraName); // GET UNIQUE SAVE PATH

        BinaryFormatter binaryFormatter = new BinaryFormatter();
        try {
            FileStream stream = new FileStream(path, FileMode.Open);

            // Deserialize STEAM
            T saveOf = (T)Convert.ChangeType(binaryFormatter.Deserialize(stream), typeof(T)); // DESERALIZE IN SERALIZED FORM
            stream.Close();

            return saveOf;// CONVERT AND RETURN TRUE FORM
        }
        catch (Exception) { // FILE DOSENT EXIST
            MonoBehaviour.print("FILEERROR");
            return save;
        }
    }
}

public class SaveSystem<T, U> where U : SaveSeralize<T>.ISaveInterface, new()
{
    public static bool IsEnabled { get { return SaveSystem.isEnabled; } }

    public void Save(T save, GameObject gameObject, string extraName = "") // SAVES THE DATA
    {
        if (!IsEnabled) { SaveSystem.ClearData(); return; } // IF NOT ENABLED, DONT WASTE THE TIME TO SAVE

        if (!Directory.Exists(BasePath)) {
            Directory.CreateDirectory(BasePath); // MAKE SURE THAT THAT FOLDER EXISTS
        }

        string path = GetPath(gameObject, typeof(U), extraName); // GET UNIQUE SAVE PATH
        BinaryFormatter binaryFormatter = new BinaryFormatter();
        FileStream stream = new FileStream(path, FileMode.Create);
        U _u = new U();
        _u.SaveData(save); // GET SERALIZED DATA
        binaryFormatter.Serialize(stream, _u);
        stream.Close();
    }

    public T Get(T save, GameObject gameObject, string extraName = "") // GETS THE DATA, PLACE ALL TYPES HERE
    {
        if (!IsEnabled) return save;

        string path = GetPath(gameObject, typeof(U), extraName); // GET UNIQUE SAVE PATH

        BinaryFormatter binaryFormatter = new BinaryFormatter();
        try {
            FileStream stream = new FileStream(path, FileMode.Open);

            // Deserialize STEAM
            U saveOf = (U)Convert.ChangeType(binaryFormatter.Deserialize(stream), typeof(U)); // DESERALIZE IN SERALIZED FORM
            stream.Close();

            saveOf.GetData(save); // GET DESERALIZED FORM
            return (T)Convert.ChangeType(saveOf, typeof(T)); // CONVERT AND RETURN TRUE FORM
        }
        catch (Exception) { // FILE DOSENT EXIST
            return save;
        }
    }
    static void print(object o)
    {
        MonoBehaviour.print(o);
    }





    static string BasePath { get { return SaveSystem.BasePath; } }
}

/// <summary>
/// Easier to write version of Generic SaveSystem
/// </summary>
public static class SaveSystem
{
    public static string GetPath(GameObject gameObject, Type u, string extraName = "") { return (BasePath + "/" + ((gameObject.scene.name)) + SaveSystem.GetObjectId(gameObject) + u.ToString() + extraName + ".save").Replace("DontDestroyOnLoad", ""); }

    class GameObjectId
    {
        public int id;
        public string UUUI;
    }

    static List<GameObjectId> gameObjectIds = new List<GameObjectId>();
    public static string GetObjectId(GameObject gameObject)
    {
        int id = gameObject.GetInstanceID();
        //List<GameObjectId> query = gameObjectIds.Where(t => t.id == id).ToList();

        bool sameId = false;
        string UUUI = "";
        for (int i = 0; i < gameObjectIds.Count; i++) {
            if (gameObjectIds[i].id == id) {
                sameId = true;
                UUUI = gameObjectIds[i].UUUI;
            }
        }
        if (!sameId) {
            gameObjectIds.Add(new GameObjectId() { id = id, UUUI = Mathf.RoundToInt((gameObject.transform.position.sqrMagnitude + gameObject.transform.rotation.eulerAngles.sqrMagnitude) * 100) + gameObject.name });
            return gameObjectIds[gameObjectIds.Count - 1].UUUI;
        }
        else {
            return UUUI;
        }
    }

    static readonly SaveSystem<Rigidbody2D, SaveSeralize<Rigidbody2D>.Rigidbody2DSaveData> rbSaveSystem = new SaveSystem<Rigidbody2D, SaveSeralize<Rigidbody2D>.Rigidbody2DSaveData>();
    static readonly SaveSystem<GameObject, SaveSeralize<GameObject>.GameObjectSaveData> goSaveSystem = new SaveSystem<GameObject, SaveSeralize<GameObject>.GameObjectSaveData>();

    public static bool isEnabled = false;

#region SaveAll
    public interface ISave
    {
        void GetAllData();
        void SaveAllData();
    }

    public static void ResetAll() // SETUP ALL WITH ISave
    {
        ISave[] s = GetSaves();
        foreach (var item in s) {
            item.GetAllData();
        }

    }

    public static void ResetAll(Scene se) // SETUP ALL WITH ISave
    {
        ISave[] s = GetSaves(se);
        foreach (var item in s) {
            item.GetAllData();
        }

    }

    static ISave[] GetSaves()
    {
        return GameObject.FindObjectsOfType<Transform>().Where(o => o.GetComponent<ISave>() != null).Select(j => j.GetComponent<ISave>()).ToArray();
    }
    static ISave[] GetSaves(Scene se)
    {
        return GameObject.FindObjectsOfType<Transform>().Where(o => o.GetComponent<ISave>() != null && o.gameObject.scene == se).Select(j => j.GetComponent<ISave>()).ToArray();
    }

    public static void SaveAll(bool createCopy = false) // SAVES ALL WITH ISave
    {
        ISave[] s = GetSaves();
        foreach (var item in s) {
            item.SaveAllData();
        }
        int folders = GetFolders();
        if (createCopy) {
            for (int i = 0; i < folders; i++) {
                Directory.Move(BasePath + (folders - i - 1), BasePath + ((folders - i)));
            }
            CopyAll(BasePath, BasePath + 0);
        }
        if (folders > maxFolders) {
            for (int i = maxFolders; i <= folders; i++) {
                Empty(new DirectoryInfo(BasePath + i));
                Directory.Delete(BasePath + i);
            }
        }
    }

    public static void LoadSave(int save, bool autoLoad = false)
    {
        try {
            Directory.Delete(BasePath);

        }
        catch (Exception) {

        }

        CopyAll(BasePath + (save), BasePath);
        if (autoLoad) {
            ResetAll();
        }
    }

    public static int GetFolders()
    {
        int folders = 0;
        bool exists = true;
        while (exists) {
            DirectoryInfo d = new DirectoryInfo(BasePath + folders);
            exists = d.Exists;
            if (exists) {
                folders++;
            }
        }
        return folders;
    }


#endregion

    public static void Save(Rigidbody2D rb, GameObject gameObject)
    {
        rbSaveSystem.Save(rb, gameObject);
    }
    public static Rigidbody2D Get(Rigidbody2D rb, GameObject gameObject)
    {
        Rigidbody2D r = rbSaveSystem.Get(rb, gameObject);
        try {
            if (!rb.isKinematic && !(rb.bodyType == RigidbodyType2D.Static)) {
                rb.velocity = r.velocity;
                rb.inertia = r.inertia;
            }
            rb.position = r.position;
            rb.rotation = r.rotation;
        }
        catch (Exception) {

        }
        return r;
    }


    public static void Save(GameObject go, GameObject gameObject)
    {
        goSaveSystem.Save(go, gameObject);
    }
    public static GameObject Get(GameObject go, GameObject gameObject)
    {
        GameObject g = goSaveSystem.Get(go, gameObject);
        go.transform.position = g.transform.position;
        go.transform.rotation = g.transform.rotation;
        go.name = g.name;
        return g;
    }
    public static void ClearData()
    {
        try {
            Empty(new DirectoryInfo(BaseFolderPath));
        }
        catch (Exception) {
        }
    }

    static void Empty(this System.IO.DirectoryInfo directory)
    {
        foreach (System.IO.FileInfo file in directory.GetFiles()) file.Delete();
        foreach (System.IO.DirectoryInfo subDirectory in directory.GetDirectories()) subDirectory.Delete(true);
    }

    public static long DirSize(DirectoryInfo d)
    {
        if (!d.Exists) return 0;
        long size = 0;

        // Add file sizes.
        FileInfo[] fis = d.GetFiles();
        foreach (FileInfo fi in fis) {
            size += fi.Length;
        }
        // Add subdirectory sizes.
        DirectoryInfo[] dis = d.GetDirectories();
        foreach (DirectoryInfo di in dis) {
            size += DirSize(di);
        }
        return size;
    }

    public static long DirSize()
    {
        return DirSize(new DirectoryInfo(BaseFolderPath));
    }

    public static string BasePath { get { return BaseFolderPath + "/SaveData"; } }
    static string BaseFolderPath { get { return Application.persistentDataPath + "/SaveFolder"; } }

    public static int maxFolders = 2; // CHANGE THIS TO HAVE SAVES

    static void CopyAll(string sourceDirectory, string targetDirectory)
    {
        var diSource = new DirectoryInfo(sourceDirectory);
        var diTarget = new DirectoryInfo(targetDirectory);

        CopyAll(diSource, diTarget);
    }

    static void CopyAll(DirectoryInfo source, DirectoryInfo target)
    {
        Directory.CreateDirectory(target.FullName);

        // Copy each file into the new directory.
        foreach (FileInfo fi in source.GetFiles()) {
            //Console.WriteLine(@"Copying {0}\{1}", target.FullName, fi.Name);
            fi.CopyTo(Path.Combine(target.FullName, fi.Name), true);
        }

        // Copy each subdirectory using recursion.
        foreach (DirectoryInfo diSourceSubDir in source.GetDirectories()) {
            DirectoryInfo nextTargetSubDir = target.CreateSubdirectory(diSourceSubDir.Name);
            CopyAll(diSourceSubDir, nextTargetSubDir);
        }
    }

}

class MonoSave : MonoBehaviour
{
    public const string publicName = "MonoSave";

    private void OnApplicationQuit()
    {
        SaveSystem.SaveAll(true);
    }

    [RuntimeInitializeOnLoadMethod]

    static void StartGame()
    {
#if !UNITY_EDITOR
        isEnabled = true;    
#endif
        SaveSystem.ResetAll();

        GameObject thisGo = new GameObject(); // creates a gameObject, will be used for Frame update
        thisGo.AddComponent<MonoSave>();
        thisGo.name = publicName;
        DontDestroyOnLoad(thisGo);
        thisGo.hideFlags = HideFlags.HideInHierarchy;

        SceneManager.sceneLoaded += SceneManager_sceneLoaded;
    }

    private static void SceneManager_sceneLoaded(Scene arg0, LoadSceneMode arg1)
    {
        SaveSystem.ResetAll(arg0);
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(MainEditor))]
public class MainObjectEditor : Editor
{
    static long size = 0;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        MainEditor me = (MainEditor)target;
        bool _save = me.SaveIsEnabled;

        me.SaveIsEnabled = EditorGUILayout.Toggle("Save Data", me.SaveIsEnabled);
        if (_save != me.SaveIsEnabled) {
            PlayerPrefs.SetInt("SaveIsEnabled", me.SaveIsEnabled ? 1 : 0);
        }


        if (GUILayout.Button("Clear Data " + size + " Kb")) {
            SaveSystem.ClearData();
            UpdateSize();
        }
        if (GUILayout.Button("Update Size")) {
            UpdateSize();
        }
        if (MainEditor.HaveFolders) {

            if (GUILayout.Button("Load Folder")) {
                LoadSave(me.loadFolder, true);
            }
        }
    }

    public static void UpdateSize()
    {
        size = SaveSystem.DirSize() / 1000;
    }
}
#endif

#if UNITY_EDITOR
public class MainEditor : MonoBehaviour
{
    [HideInInspector] public bool SaveIsEnabled = true;
    public int loadFolder = 0;
    public static bool HaveFolders { get { return SaveSystem.GetFolders() > 0; } }
    private void OnValidate()
    {
        int max = SaveSystem.GetFolders() - 1;
        if (loadFolder < 0) { loadFolder = 0; }
        else if (loadFolder > max) { loadFolder = max; }
    }

    private void Awake()
    {
        SaveIsEnabled = PlayerPrefs.GetInt("SaveIsEnabled", 0) == 1;
        SaveSystem.isEnabled = SaveIsEnabled;
        MainObjectEditor.UpdateSize();
    }

    private void OnApplicationQuit()
    {
        MainObjectEditor.UpdateSize();
    }
}
#endif


#endif