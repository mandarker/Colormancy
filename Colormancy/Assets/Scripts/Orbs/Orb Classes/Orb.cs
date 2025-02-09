﻿using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public abstract class Orb
{
    [System.Serializable]
    public enum SpellShape
    {
        Jump, Fireball, OrbitingOrbs, Vines, Ink, Cloud, Shockwave, Bolt, ExpandingOrbs
    }

    [System.Serializable]
    public enum Element
    {
        Wrath, Fire, Light, Nature, Water, Poison, Earth, Wind, Darkness, AutoAttack
    }

    #region Orb Values
    protected SpellShape m_OrbShape;
    protected Element m_OrbElement;
    protected int m_Level = 1;

    protected PhotonView m_casterView = null; // the PhotonView of the caster, used to prevent self damage
    protected bool m_pvpEnabled = false;

    public GameObject m_UIPrefab;
    #endregion

    // getters and setters for protection
    #region Getters
    public SpellShape getShape() { return m_OrbShape; }
    public Element getElement() { return m_OrbElement; }
    public int getLevel() { return m_Level; }
    public PhotonView getCasterPView() { return m_casterView; }
    public bool getPVPStatus() { return m_pvpEnabled; }
    #endregion

    #region Setters
    public void setShape(SpellShape e) { m_OrbShape = e; }
    public void setElement( Element e ) { m_OrbElement = e; }
    public void setLevel( int l ) { m_Level = l; }
    public void setCasterPView(PhotonView newView ) { m_casterView = newView; }
    public void setPVPStatus(bool newStatus) { m_pvpEnabled = newStatus; }
    #endregion

    //SpellTest will just be the player controller
    public delegate void GreaterCast(GameObject hit, float spellEffectMod, float[] data, Transform casterTransform);
    public delegate void LesserCast(GameObject hit, float spellEffectMod, float[] data);

    public abstract void CastShape(GreaterCast greaterEffectMethod, LesserCast lesserEffectMethod, Transform t, Vector3 clickedPosition, float spellDamageMultiplier);
    public abstract void CastGreaterEffect(GameObject hit, float spellEffectMod, float[] data, Transform casterTransform);
    //Will have to do something different and send over server for this one since most are for allies
    public abstract void CastLesserEffect(GameObject hit, float spellEffectMod, float[] data);
    public abstract void RevertHeldEffect(GameObject player);
    public abstract void AddHeldEffect(GameObject player);
}
