﻿using UnityEngine;
using System.Collections;

public class EnemyController : MonoBehaviour {
	public float visionDistance = 15; 		// How far the enemy can see
	public float followSpeed = 12;
	public AudioClip followAudioClip;
	public float pathTime = 10; 			// Time taken for enemy to traverse its path
	public float respawnTime = 1;			// Time until enemy respawns after death. Will not respawn if set to 0.
	public float knockbackVelocity = 30;	// Distance of how much a victim is knocked back on collission with enemy
	public float knockbackDuration = 1;		// Duration of enemy being knocked back

	protected NavMeshAgent agent;
	protected GameObject player;
	protected SixenseHandController[] playerHandControllers;
	protected RaycastHit hit;
	protected Movement movement;
	protected string initAnimationName;
	protected string pathName;
	protected float pathTimer;
	protected float speed;
	protected float defaultSpeed;
	protected bool heldByPlayer; 								// If enemy has been held by player before
	protected bool knockingBack;								// If enemy is currently being knocked back
	protected bool dead; 										// If enemy is dead

	// These are all the movement types that the enemy can do
	protected enum Movement{Path, Follow, Freeze};

	protected Vector3 spawnPosition;
	protected Quaternion spawnRotation;

	protected virtual void Awake() {
		agent = this.GetComponent<NavMeshAgent> ();
		player = GameObject.FindWithTag("Player");
		playerHandControllers = player.GetComponentsInChildren<SixenseHandController> ();
		pathName = this.GetComponent<iTweenPath> ().pathName;
		defaultSpeed = agent.speed;
		initAnimationName = animation.clip.name;
		spawnPosition = transform.position;
		spawnRotation = transform.rotation;
	}

	protected virtual void Start() {
		Init ();
	}

	protected virtual void Init () {
		movement = Movement.Path;
		pathTimer = 0;
		heldByPlayer = false;
		knockingBack = false;
		dead = false;
	}

	protected virtual void Update () {
		if (!player || dead) {
			return;
		}

		pathTimer -= Time.deltaTime;

		IsPlayerHoldingEnemy ();

		switch (movement)
		{
			case Movement.Follow:
				FollowPlayer ();
				break;
			case Movement.Freeze:
				Freeze ();
				break;
			case Movement.Path:
				PathMovement();
				break;
		}

		if (movement == Movement.Path) {
			IsObjectInViewByTag("Player");
		}
	}
	
	// If player is holding enemy then stop any enemy movement
	protected void IsPlayerHoldingEnemy() {
		bool isHoldingEnemy = false;
		foreach (SixenseHandController playerHandController in playerHandControllers) {
			if (gameObject == playerHandController.GetClosestObject() && playerHandController.IsHoldingObject()) {
				isHoldingEnemy = true;
				heldByPlayer = true;
			}
		}

		if (isHoldingEnemy) {
			movement = Movement.Freeze;
			agent.enabled = false;
		} else {
			agent.enabled = true;
		}

		if (!isHoldingEnemy && heldByPlayer) {
			agent.enabled = true;
			movement = Movement.Follow;
		}
	}

	protected virtual void FollowPlayer () {
		iTween.Stop(gameObject);
		agent.SetDestination (player.transform.position);
		agent.speed = followSpeed;

		if (!audio.isPlaying) {
			audio.clip = followAudioClip;
			audio.Play();
		}
	}

	// Check if certain object is in view of enemy. Object identified by its tag
	protected void IsObjectInViewByTag( string tag) {
		if (Physics.Raycast(transform.position, this.transform.forward, out hit)) {
			if (hit.transform.tag == tag && hit.distance <= visionDistance ) {
				movement = Movement.Follow;
			}
		}
	}

	protected void PathMovement() {
		if (pathTimer <= 0) {
			pathTimer = pathTime * 60;
			movement = Movement.Path;
			PathAction();
		}
	}

	protected void PathAction () {
		iTween.MoveTo(gameObject,
		              iTween.Hash("path", iTweenPath.GetPath(pathName),
		            "time", pathTime,
		            "easetype", iTween.EaseType.linear,
		            "looptype", iTween.LoopType.loop,
		            "orienttopath", true));
	}

	protected virtual void Freeze () {
		iTween.Stop(gameObject);
	}
	
	protected virtual void Knockback(GameObject knocker, GameObject victim) {
		Vector3 dir = (victim.transform.position - knocker.transform.position).normalized;
		dir.y = 0;

		if (victim == player) {
			CharacterMotor charMotor = player.GetComponent<CharacterMotor> ();
			charMotor.SetVelocity (dir * knockbackVelocity);
		} else if (victim.rigidbody && !knockingBack) {
			movement = Movement.Freeze;
			victim.rigidbody.AddForce(dir * knockbackVelocity * 10);			
			knockingBack = true;
			StartCoroutine(KnockbackEnemy(knockbackDuration));
		}
	}
	
	protected IEnumerator KnockbackEnemy (float length) {
		yield return new WaitForSeconds(length);
		rigidbody.velocity = Vector3.zero;
		rigidbody.angularVelocity = Vector3.zero;
		rigidbody.Sleep();
		knockingBack = false;
		dead = true;
		ToggleVisibility ();
		StartCoroutine(Death(1));
	}
	
	protected IEnumerator Death (float length) {
		dead = true;
		yield return new WaitForSeconds(length);
		if (respawnTime >= 0) {
			StartCoroutine(Respawn(respawnTime));
		}
	}

	protected IEnumerator Respawn (float length) {		
		yield return new WaitForSeconds(length);
		gameObject.transform.position = spawnPosition;
		gameObject.transform.rotation = spawnRotation;
		agent.speed = defaultSpeed;
		animation.Play (initAnimationName);
		Init ();
		ToggleVisibility ();
	}
	
	protected void ToggleVisibility() {
		Renderer[] renderers = gameObject.GetComponentsInChildren<Renderer>();
		foreach (Renderer renderer in renderers) {
			renderer.enabled = !renderer.enabled;
		}

		Collider[] colliders = gameObject.GetComponentsInChildren<Collider>();
		foreach (Collider col in colliders) {
			col.enabled = !col.enabled;
		}
	}
}
