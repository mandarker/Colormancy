﻿using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OrangeSpellController : MonoBehaviour
{
    public Orb.GreaterCast greaterCast;
    public Orb.LesserCast lesserCast;
    public float spellEffectMod;
    private Orb.Element element = Orb.Element.Fire;

    public bool PVPEnabled = false;
    public PhotonView CasterPView = null;

    [Space]

    [SerializeField]
    private float speed;
    private float startTime;
    [SerializeField]
    private float lifetime;
    
    [Space]

    [SerializeField]
    private bool debug;

    // Start is called before the first frame update
    void Start()
    {
        startTime = Time.time;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (debug)
        {
            float xpos = 5 * Mathf.Sin(Time.time);
            float ypos = 5 * Mathf.Cos(Time.time);

            transform.position = new Vector3(xpos, 1, ypos);
        }
        else
        {
            transform.position += transform.forward * speed;

            if (Time.time - startTime > lifetime && !debug)
                Destroy(gameObject);
        }

        PaintingManager.PaintSphere(OrbValueManager.getColor(element), transform.position, OrbValueManager.getPaintRadius(element));
    }

    private void OnCollisionEnter(Collision collision)
    {
        float paintRadius = OrbValueManager.getPaintRadius(element);

        PaintingManager.PaintSphere(OrbValueManager.getColor(element), collision.GetContact(0).point, paintRadius * 3);

        Collider[] sphereCollisions = Physics.OverlapSphere(collision.GetContact(0).point, paintRadius * 3, (1 << 10) | (1 << 9));
        foreach (Collider c in sphereCollisions)
        {
            if (c.gameObject.CompareTag("Enemy"))
                greaterCast(c.gameObject, spellEffectMod, null, CasterPView.transform);
            else if (c.gameObject.CompareTag("Player"))
            {
                PhotonView p = PhotonView.Get(c.gameObject);
                
                if (PVPEnabled && PhotonView.Get(c.gameObject).ViewID != CasterPView.ViewID)
                {
                    greaterCast(c.gameObject, spellEffectMod, null, CasterPView.transform);
                }
                else
                {
                    lesserCast(c.gameObject, spellEffectMod, null);
                }
            }
        }

        Destroy(gameObject);
    }
}
