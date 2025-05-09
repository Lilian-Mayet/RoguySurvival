using UnityEngine;
using System.Collections.Generic;
public enum WeaponType
{
    Sword,
    Hammer,
    Knife,
    Axe,
    Wand,
    Spear,
    None // For no weapon equipped
}

[System.Serializable]
public class WeaponStats
{
    public string weaponID; // Unique ID, e.g., "sword_001_common_iron"
    public string itemName; // Display name, e.g., "Iron Sword"
    public WeaponType weaponType;
    public int damage;
    public float attackSpeed; // e.g., attacks per second, or time between attacks
    public float weight;
    public string description;
    // public Sprite icon; // We'll handle sprite loading separately from JSON for now
    public string iconSpriteName; // Name of the sprite in your resources or asset bundle
                                  // This will match the name you gave when slicing the spritesheet.
                                  // e.g., "weapons_0" if your sliced sprite is named that.

    // Constructor (optional, but can be useful)
    public WeaponStats(string id, string name, WeaponType type, int dmg, float speed, float wgt, string desc, string spriteName)
    {
        weaponID = id;
        itemName = name;
        weaponType = type;
        damage = dmg;
        attackSpeed = speed;
        weight = wgt;
        description = desc;
        iconSpriteName = spriteName;
    }

    // Default constructor for serialization
    public WeaponStats() { }
}

[System.Serializable]
public class WeaponDatabase
{
    public List<WeaponStats> allWeapons;

    public WeaponDatabase()
    {
        allWeapons = new List<WeaponStats>();
    }
}