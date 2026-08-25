using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using TMPro;

public class PlacementLogger : MonoBehaviour
{
    [Header("Participant / condition")]
    public string participantId = "P01";
    public string condition = "AR";   // use "2D" in the top-down app

    [Header("Debug overlay (optional but recommended on Android)")]
    public TMP_Text debugText;

    private readonly List<GameObject> tracked = new List<GameObject>();
    private readonly Dictionary<GameObject, string> ids = new Dictionary<GameObject, string>();
    private readonly Dictionary<string, int> nameCounts = new Dictionary<string, int>();

    private int adjustments = 0;
    private float startTime;
    private bool timing = false;

    private string lastSavedFile = null; // tracked so the Share button knows what to send

    public void StartTask()
    {
        adjustments = 0;
        startTime = Time.time;
        timing = true;
    }

    public void RegisterPlacement(GameObject obj, string prefabName)
    {
        if (obj == null) return;
        if (!timing) { startTime = Time.time; timing = true; }

        int n = nameCounts.ContainsKey(prefabName) ? nameCounts[prefabName] + 1 : 1;
        nameCounts[prefabName] = n;
        string id = (n == 1) ? prefabName : prefabName + "#" + n;

        tracked.Add(obj);
        ids[obj] = id;
    }

    public void UnregisterPlacement(GameObject obj)
    {
        if (obj == null) return;
        tracked.Remove(obj);
        ids.Remove(obj);
    }

    public void UnregisterAll()
    {
        tracked.Clear();
        ids.Clear();
        nameCounts.Clear();
    }

    public void CountAdjustment()
    {
        adjustments++;
    }

    public void Export()
    {
        float elapsed = timing ? Time.time - startTime : 0f;
        string stamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

        Vector3 origin = Vector3.zero;
        bool haveOrigin = false;
        foreach (var o in tracked)
        {
            if (o != null) { origin = o.transform.position; haveOrigin = true; break; }
        }

        var sb = new StringBuilder();
        sb.AppendLine("participant_id,condition,object_id,x_cm,y_cm,rotation_deg,scale," +
                      "adjustments,time_seconds,world_x_m,world_y_m,world_z_m,timestamp");

        foreach (var o in tracked)
        {
            if (o == null) continue;
            Vector3 p = o.transform.position;

            float xCm = haveOrigin ? (p.x - origin.x) * 100f : p.x * 100f;
            float yCm = haveOrigin ? (p.z - origin.z) * 100f : p.z * 100f;
            float rot = o.transform.eulerAngles.y;
            float scl = o.transform.localScale.x;
            string id = ids.ContainsKey(o) ? ids[o] : o.name.Replace("(Clone)", "");

            sb.AppendLine($"{participantId},{condition},{id}," +
                          $"{xCm:F1},{yCm:F1},{rot:F1},{scl:F2}," +
                          $"{adjustments},{elapsed:F1}," +
                          $"{p.x:F3},{p.y:F3},{p.z:F3},{stamp}");
        }

        if (tracked.Count == 0)
        {
            SetDebugText("No objects tracked — nothing saved.");
            Debug.LogWarning("[PlacementLogger] Export called with 0 tracked objects — skipped.");
            return;
        }

        string fileName = $"placement_{participantId}_{condition}_{stamp}.csv";
        string file = Path.Combine(Application.persistentDataPath, fileName);
        File.WriteAllText(file, sb.ToString());

        lastSavedFile = file;

        Debug.Log("[PlacementLogger] Saved to: " + file);
        SetDebugText($"Saved: {fileName}\n({tracked.Count} objects)");
        GUIUtility.systemCopyBuffer = file;
    }

    // Wire a UI Button's OnClick to this. Fires Android's native share sheet
    // with the CSV from the most recent Export() call.
    public void ShareLastFile()
    {
        if (string.IsNullOrEmpty(lastSavedFile) || !File.Exists(lastSavedFile))
        {
            SetDebugText("Nothing to share yet — export first.");
            Debug.LogWarning("[PlacementLogger] ShareLastFile called with no valid file.");
            return;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            // FileProvider setup (see instructions) is required for this to work
            // on Android 7.0+ — raw file:// URIs are blocked by the OS.
            string authority = Application.identifier + ".fileprovider";

            AndroidJavaClass fileProviderClass = new AndroidJavaClass("androidx.core.content.FileProvider");
            AndroidJavaObject fileObj = new AndroidJavaObject("java.io.File", lastSavedFile);

            AndroidJavaObject currentActivity = new AndroidJavaClass("com.unity3d.player.UnityPlayer")
                .GetStatic<AndroidJavaObject>("currentActivity");

            AndroidJavaObject uri = fileProviderClass.CallStatic<AndroidJavaObject>(
                "getUriForFile", currentActivity, authority, fileObj);

            AndroidJavaObject intentObject = new AndroidJavaObject("android.content.Intent");
            intentObject.Call<AndroidJavaObject>("setAction", "android.intent.action.SEND");
            intentObject.Call<AndroidJavaObject>("putExtra", "android.intent.extra.STREAM", uri);
            intentObject.Call<AndroidJavaObject>("setType", "text/csv");
            intentObject.Call<AndroidJavaObject>("addFlags", 1); // FLAG_GRANT_READ_URI_PERMISSION

            AndroidJavaClass intentClass = new AndroidJavaClass("android.content.Intent");
            AndroidJavaObject chooser = intentClass.CallStatic<AndroidJavaObject>(
                "createChooser", intentObject, "Share placement data");

            currentActivity.Call("startActivity", chooser);
            SetDebugText("Opening share sheet…");
        }
        catch (System.Exception e)
        {
            Debug.LogError("[PlacementLogger] Share failed: " + e.Message);
            SetDebugText("Share failed:\n" + e.Message);
        }
#else
        Debug.Log("[PlacementLogger] Share only works on an Android device build (not Editor).");
        SetDebugText("Share only works on-device (Android build).");
#endif
    }

    private void SetDebugText(string msg)
    {
        if (debugText != null) debugText.text = msg;
    }
}