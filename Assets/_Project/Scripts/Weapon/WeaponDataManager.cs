// WeaponDataManager.cs
using UnityEngine;
using System.Collections.Generic;
using System.IO; // For file operations
using System.Linq; // For Linq queries like FirstOrDefault

public class WeaponDataManager : MonoBehaviour
{
    public static WeaponDataManager Instance { get; private set; }

    public Dictionary<string, WeaponStats> weapons = new Dictionary<string, WeaponStats>();
    // It's also useful to have a list if you need to iterate through all of them
    public List<WeaponStats> weaponList = new List<WeaponStats>();

    // Path to your sprites. This example assumes they are in a "Resources/WeaponIcons/" folder.
    // Adjust if you use Addressables or Asset Bundles.
    private const string WEAPON_ICON_RESOURCE_PATH = "WeaponIcons/";

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Make it persistent across scenes
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
            Debug.Log(loadedData);

            if (loadedData != null && loadedData.allWeapons != null)
            {
                foreach (WeaponStats weapon in loadedData.allWeapons)
                {
                    if (!weapons.ContainsKey(weapon.weaponID))
                    {
                        weapons.Add(weapon.weaponID, weapon);
                        weaponList.Add(weapon); // Also add to the list
                    }
                    else
                    {
                        Debug.LogWarning($"WeaponDataManager: Duplicate weaponID found: {weapon.weaponID}");
                    }
                }
                Debug.Log($"WeaponDataManager: Loaded {weapons.Count} weapons from JSON.");
            }
            else
            {
                Debug.LogError("WeaponDataManager: Failed to parse weapon data from JSON or file is empty/corrupted.");
            }
        }
        else
        {
            Debug.Log($"WeaponDataManager: Cannot find Weapons.json at path: {filePath}");
        }
    }

    public WeaponStats GetWeaponStats(string weaponID)
    {
        if (weapons.TryGetValue(weaponID, out WeaponStats stats))
        {
            return stats;
        }
        Debug.LogWarning($"WeaponDataManager: Weapon with ID '{weaponID}' not found.");
        return null;
    }

    public Sprite GetWeaponIcon(string weaponType,string iconSpriteName)
    {
        if (string.IsNullOrEmpty(iconSpriteName))
        {
            Debug.Log("NameNull");
            return null;
        }
        // This loads from a "Resources" folder.
        // Your spritesheet (already sliced) for weapon icons should be in Assets/Resources/WeaponIcons/
        // and the iconSpriteName should be the name of the individual sprite.
        Sprite icon = Resources.Load<Sprite>(WEAPON_ICON_RESOURCE_PATH + weaponType +"/"+ iconSpriteName+".png");
        Debug.Log(WEAPON_ICON_RESOURCE_PATH + weaponType +"/"+ iconSpriteName);
        
        if (icon == null)
        {
            
            Debug.LogWarning($"WeaponDataManager: Could not load sprite '{iconSpriteName}' from Resources path '{WEAPON_ICON_RESOURCE_PATH}'. Make sure it's in a Resources folder and the name is correct.");
        }
        return icon;
    }
}