// WeaponDataManager.cs
using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class WeaponDataManager : MonoBehaviour
{
    public static WeaponDataManager Instance { get; private set; }

    public Dictionary<string, WeaponStats> weapons = new Dictionary<string, WeaponStats>();
    public List<WeaponStats> weaponList = new List<WeaponStats>();

    public string weaponIconFolderPathInResources = "WeaponIcons/";

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);



            LoadWeaponData();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    

    void LoadWeaponData()
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, "Weapons.json");
        if (File.Exists(filePath))
        {
            string dataAsJson = File.ReadAllText(filePath);
            WeaponDatabase loadedData = JsonUtility.FromJson<WeaponDatabase>(dataAsJson);
            if (loadedData != null && loadedData.allWeapons != null)
            {
                foreach (WeaponStats weapon in loadedData.allWeapons)
                {
                    if (!weapons.ContainsKey(weapon.weaponID))
                    {
                        weapons.Add(weapon.weaponID, weapon);
                        weaponList.Add(weapon);
                    }
                    else { Debug.LogWarning($"Duplicate weaponID: {weapon.weaponID}"); }
                }
                Debug.Log($"WeaponDataManager: Loaded {weapons.Count} weapons.");
            } else { Debug.LogError("WeaponDataManager: Failed to parse JSON."); }
        } else { Debug.LogError($"WeaponDataManager: Cannot find {filePath}"); }
    }

    public WeaponStats GetWeaponStats(string weaponID)
    {
        weapons.TryGetValue(weaponID, out WeaponStats stats);
        if (stats == null) Debug.LogWarning($"Weapon with ID '{weaponID}' not found.");
        return stats;
    }

    public Sprite GetWeaponIcon(string weaponType,string iconSpriteFileName)
    {
        if (string.IsNullOrEmpty(iconSpriteFileName))
        {
            Debug.LogWarning("WeaponDataManager: iconSpriteFileName is null or empty.");
            return null;
        }
        if (string.IsNullOrEmpty(weaponIconFolderPathInResources))
        {
            Debug.LogError("WeaponDataManager: weaponIconFolderPathInResources is not set!");
            return null;
        }

        string spriteNameWithoutExtension = Path.GetFileNameWithoutExtension(iconSpriteFileName);
        string resourcePath = weaponIconFolderPathInResources +  weaponType  + "/" + spriteNameWithoutExtension;

        // Debug.Log($"Attempting to load sprite from Resources with path: '{resourcePath}' (original iconSpriteFileName: '{iconSpriteFileName}')"); // Vous pouvez garder ce log si utile

        Sprite icon = Resources.Load<Sprite>(resourcePath);

        if (icon == null)
        {
            Debug.LogWarning($"WeaponDataManager: Could not load sprite from Resources at path: '{resourcePath}'. Ensure the file exists at 'Assets/Resources/{resourcePath}.png' (or other image format) and its Texture Type is 'Sprite (2D and UI)'. Also check for typos in 'iconSpriteName' in your JSON.");
        }
        return icon;
    }
}