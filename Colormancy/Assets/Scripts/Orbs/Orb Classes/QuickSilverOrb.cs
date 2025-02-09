﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

using Photon.Pun;
public class QuickSilverOrb : Orb
{
    public QuickSilverOrb()
    {
        m_OrbShape = SpellShape.Bolt;
        m_OrbElement = Element.Wind;
        m_UIPrefab = (GameObject)Resources.Load("Orbs/QuickSilverOrbUI");
    }

    public override void AddHeldEffect(GameObject player)
    {
        PlayerMovement move = player.GetComponent<PlayerMovement>();
        float percent = ((100 + OrbValueManager.getHoldIncreaseValue(m_OrbElement)) / 100);
        move.AlterWalkSpeed(move.WalkSpeed * percent);
        move.AlterRunSpeed(move.RunSpeed * percent);

        PhotonView photonView = PhotonView.Get(player);
        photonView.RPC("IncreaseHealth", RpcTarget.All, -OrbValueManager.getHoldDecreaseValue(m_OrbElement));
    }

    public override void RevertHeldEffect(GameObject player)
    {
        PlayerMovement move = player.GetComponent<PlayerMovement>();
        float percent = ((100 + OrbValueManager.getHoldIncreaseValue(m_OrbElement)) / 100);
        move.AlterWalkSpeed(move.WalkSpeed / percent);
        move.AlterRunSpeed(move.RunSpeed / percent);

        PhotonView photonView = PhotonView.Get(player);
        photonView.RPC("IncreaseHealth", RpcTarget.All, OrbValueManager.getHoldDecreaseValue(m_OrbElement));
    }
    public override void CastGreaterEffect(GameObject hit, float spellEffectMod, float[] data, Transform casterTransform)
    {
        float dmgMultiplier = 1;
        if (hit.GetComponent<StatusEffectScript>().StatusExists(StatusEffect.StatusType.SpellIncreasedDamage))
            dmgMultiplier += OrbValueManager.getGreaterEffectPercentile(Element.Water) / 100f;

        PhotonView photonView = PhotonView.Get(hit);
        casterTransform.GetComponent<ItemManager>().DamageDealt(hit, casterTransform);
        photonView.RPC("TakeDamage", RpcTarget.All, OrbValueManager.getGreaterEffectDamage(m_OrbElement, m_Level) * spellEffectMod * dmgMultiplier);
    }

    public override void CastLesserEffect(GameObject hit, float spellEffectMod, float[] data)
    {
        StatusEffectScript status = hit.GetComponent<StatusEffectScript>();
        status.RPCApplyStatus(StatusEffect.StatusType.MovementIncreaseSpeed, 0, 0, OrbValueManager.getLesserEffectValue(m_OrbElement, m_Level));
    }

    public override void CastShape(GreaterCast greaterEffectMethod, LesserCast lesserEffectMethod, Transform t, Vector3 clickedPosition, float spellDamageMultiplier)
    {
        Transform wizard = t.GetChild(0);

        Vector3 direction = clickedPosition - t.position;
        wizard.LookAt(wizard.position + new Vector3(direction.x, 0, direction.y).normalized);

        GameObject g = GameObject.Instantiate(Resources.Load("Orbs/QuickSilver Bolt"), clickedPosition + Vector3.up * 0.03f, t.rotation) as GameObject;
        QuickSilverSpellController spellController = g.GetComponent<QuickSilverSpellController>();

        spellController.greaterCast = greaterEffectMethod;
        spellController.lesserCast = lesserEffectMethod;
        spellController.spellEffectMod = OrbValueManager.getShapeEffectMod(m_OrbElement) * spellDamageMultiplier;
        spellController.PVPEnabled = getPVPStatus();
        spellController.CasterPView = getCasterPView();

        if (getPVPStatus())
        {
            g.layer = LayerMask.NameToLayer("Default");
        }
    }

    public static object Deserialize(byte[] data)
    {
        QuickSilverOrb result = new QuickSilverOrb();
        result.setLevel(data[0]);
        return result;
    }

    public static byte[] Serialize(object customType)
    {
        QuickSilverOrb o = (QuickSilverOrb)customType;
        return new byte[] { (byte)o.getLevel() };
    }
}
