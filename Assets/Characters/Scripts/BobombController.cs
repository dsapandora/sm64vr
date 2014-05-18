﻿using UnityEngine;
using System.Collections;

public class BobombController : EnemyController {
	public float deathTimer = 5; // Seconds until bobomb explodes
	public AudioClip explosionAudioClip;
	
	private Transform smoke;
	private GameObject explosion;
	private float defaultDeathTimer;
	
	protected override void Awake() {
		base.Awake ();
		smoke = transform.Find ("Smoke");
		defaultDeathTimer = deathTimer;
	}
	
	protected override void Init() {
		deathTimer = defaultDeathTimer;
		base.Init ();
	}
	
	protected override void FollowPlayer() {
		base.FollowPlayer ();
		Detonation ();
	}
	
	protected override void Freeze() {
		base.Freeze ();

		if (!audio.isPlaying) {
			audio.clip = followAudioClip;
			audio.Play();
		}

		Detonation ();
	}

	protected void Detonation () {		
		if (!smoke.particleSystem.isPlaying) {			
			smoke.particleSystem.Play ();
		}
		
		if (deathTimer <= 0) {
			animation.Play("Explode");
			StartCoroutine(Explode(animation["Explode"].length));
		} else {
			deathTimer -= Time.deltaTime;
		}
	}

	protected IEnumerator Explode (float length) {
		explosion = (GameObject) Instantiate(Resources.Load("Explosion"));
		explosion.transform.position = transform.position;
		dead = true;
		yield return new WaitForSeconds(length);
		audio.clip = explosionAudioClip;
		audio.Play();
		smoke.particleSystem.Stop ();
		ToggleVisibility ();
		StartCoroutine(Death(explosionAudioClip.length));
	}
}