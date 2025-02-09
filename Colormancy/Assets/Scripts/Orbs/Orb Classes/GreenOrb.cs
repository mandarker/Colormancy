﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

using Photon.Pun;
public class GreenOrb : Orb
{
    public GreenOrb()
    {
        m_OrbShape = SpellShape.Vines;
        m_OrbElement = Element.Nature;
        m_UIPrefab = (GameObject)Resources.Load("Orbs/GreenOrbUI");
    }

    public override void AddHeldEffect(GameObject player)
    {
        PhotonView photonView = PhotonView.Get(player);
        photonView.RPC("IncreaseHealth", RpcTarget.All, OrbValueManager.getHoldIncreaseValue(m_OrbElement));

        PlayerMovement move = player.GetComponent<PlayerMovement>();
        float percent = ((100 - OrbValueManager.getHoldDecreaseValue(m_OrbElement)) / 100);
        move.AlterWalkSpeed(move.WalkSpeed * percent);
        move.AlterRunSpeed(move.RunSpeed * percent);
    }

    public override void RevertHeldEffect(GameObject player)
    {
        PhotonView photonView = PhotonView.Get(player);
        photonView.RPC("IncreaseHealth", RpcTarget.All, -OrbValueManager.getHoldIncreaseValue(m_OrbElement));

        PlayerMovement move = player.GetComponent<PlayerMovement>();
        float percent = ((100 - OrbValueManager.getHoldDecreaseValue(m_OrbElement)) / 100);
        move.AlterWalkSpeed(move.WalkSpeed / percent);
        move.AlterRunSpeed(move.RunSpeed / percent);
    }

    public override void CastGreaterEffect(GameObject hit, float spellEffectMod, float[] data, Transform casterTransform)
    {
        float dmgMultiplier = 1;
        if (hit.GetComponent<StatusEffectScript>().StatusExists(StatusEffect.StatusType.SpellIncreasedDamage))
            dmgMultiplier += OrbValueManager.getGreaterEffectPercentile(Element.Water) / 100f;

        PhotonView photonView = PhotonView.Get(hit);
        casterTransform.GetComponent<ItemManager>().DamageDealt(hit, casterTransform);
        photonView.RPC("TakeDamage", RpcTarget.All, OrbValueManager.getGreaterEffectDamage(m_OrbElement, m_Level) * spellEffectMod * dmgMultiplier);

        StatusEffectScript script = hit.GetComponent<StatusEffectScript>();
        script.RPCApplyStatus(StatusEffect.StatusType.Slowdown, OrbValueManager.getGreaterEffectDuration(m_OrbElement, m_Level), 0, 80, "green_orb");
    }

    public override void CastLesserEffect(GameObject hit, float spellEffectMod, float[] data)
    {
        StatusEffectScript status = hit.GetComponent<StatusEffectScript>();
        status.RPCApplyStatus(StatusEffect.StatusType.Rejuvenation, OrbValueManager.getLesserEffectDuration(m_OrbElement, m_Level), 1, OrbValueManager.getLesserEffectValue(m_OrbElement, m_Level), "green_orb");
    }

    public override void CastShape(GreaterCast greaterEffectMethod, LesserCast lesserEffectMethod, Transform t, Vector3 clickedPosition, float spellDamageMultiplier)
    {
        Transform wizard = t.GetChild(0);

        Vector3 direction = new Vector3(clickedPosition.x - t.position.x, 0, clickedPosition.z - t.position.z).normalized;
        wizard.LookAt(wizard.position + direction);

        GameObject g = GameObject.Instantiate(Resources.Load("Orbs/Green Vine Spawner"), t.position, wizard.rotation) as GameObject;
        GreenSpellSpawnerController spellController = g.GetComponent<GreenSpellSpawnerController>();

        spellController.greaterCast = greaterEffectMethod;
        spellController.lesserCast = lesserEffectMethod;
        spellController.spellEffectMod = OrbValueManager.getShapeEffectMod(m_OrbElement) * spellDamageMultiplier;
        spellController.PVPEnabled = getPVPStatus();
        spellController.CasterPView = getCasterPView();
    }

    public static object Deserialize(byte[] data)
    {
        GreenOrb result = new GreenOrb();
        result.setLevel(data[0]);
        return result;
    }

    public static byte[] Serialize(object customType)
    {
        GreenOrb o = (GreenOrb)customType;
        return new byte[] { (byte)o.getLevel() };
    }
}
