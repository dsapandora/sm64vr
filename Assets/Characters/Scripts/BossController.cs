﻿using UnityEngine;
using System.Collections;

public class BossController : MonoBehaviour {
	public float health = 3;
	public float followSpeed = 3;
	public float followAngularSpeed = 30;
	public float playerCarrySpeed = 5;							// Walking speed of player when carrying boss
	public float playerCarryTurnSpeed = 2;						// Turn speed of player when carrying boss
	public float minHurtAltitude = 0;							// The min altitude that boss must be at to be hurt by player
	public float heldFixedRotationX;							// Keep the boss rotated on axis X at this value when held by player
	public string heldAnimationName;							// Name of animation clip when player holds boss
	public float heldAnimationSpeed;							// How fast animation runs when boss held by player
	public string grabAnimationName;							// Name of animation clip when boss grabs player
	public string throwAnimationName;							// Name of animation clip when boss throws player
	public GameObject terrain;									// Terrain that the boss stands on
	public GameObject grabPerimeter;							// Area where boss will grab player if player enters the area.
	public float minDistanceGrabPermimeter = 4;					// If player distance from grab perimeter less then this, player is grabbed.
	public AudioClip hurtAudioClip;								// Sound when boss gets hurt
	
	protected NavMeshAgent agent;
	protected GameObject player;
	protected CharacterMotor motor;
	protected FPSInputController playerController;
	protected PlayerLook playerLook;
	protected HydraLook playerHydraLook;
	protected PlayerHealth playerHealth;
	protected HandController[] playerHandControllers;
	protected Movement movement;
	protected float defaultHealth;
	protected float defaultSpeed;
	protected float defaultAngularSpeed;
	protected bool initBattle = false;
	protected bool startedBattle = false;
	protected bool isHeldByPlayer;	 							// If boss is currently being held by player
	protected bool wasHeldByPlayer; 							// If boss has been held by player before
	protected bool isBeingHurt;
	protected bool isThrowingPlayer = false;

	// These are all the movement types that the enemy can do
	protected enum Movement{Follow, Freeze, Grab, Idle, Throw};
	
	protected GameObject startMarkerThrowPlayer;
	protected GameObject endMarkerThrowPlayer;
	protected float startTimeThrowPlayer;
	protected float journeyLengthThrowPlayer;
	
	protected virtual void Awake () {
		agent = this.GetComponent<NavMeshAgent> ();
		player = GameObject.FindWithTag("Player");
		motor = player.GetComponent<CharacterMotor> ();
		playerController = player.GetComponent<FPSInputController> ();
		playerLook = player.GetComponent<PlayerLook> ();
		playerHydraLook = player.GetComponent<HydraLook> ();
		playerHealth = player.GetComponent<PlayerHealth> ();
		playerHandControllers = player.GetComponentsInChildren<HandController> ();
		startMarkerThrowPlayer = new GameObject();
		endMarkerThrowPlayer = new GameObject ();
		defaultHealth = health;
		defaultSpeed = agent.speed;
		defaultAngularSpeed = agent.angularSpeed;
		movement = Movement.Idle;
	}

	protected virtual void Start () {
		health = defaultHealth;
		
		isHeldByPlayer = false;
		wasHeldByPlayer = false;
		isBeingHurt = false;

		TriggerIgnorePlayerHandColliders (true);
	}
	
	protected virtual void Update () {
		if (initBattle && !startedBattle) {
			StartBattle();
		}
		
		IsPlayerHoldingEnemy ();

		IsPlayerInGrabPermimeter ();

		switch (movement)
		{
		case Movement.Follow:
			FollowPlayer ();
			break;
		case Movement.Freeze:
			Freeze ();
			break;
		}

		if (isThrowingPlayer) {
			float distCovered = (Time.time - startTimeThrowPlayer) * 20.0f;
			float fracJourney = distCovered / journeyLengthThrowPlayer;
			player.transform.position = Vector3.Lerp(startMarkerThrowPlayer.transform.position, endMarkerThrowPlayer.transform.position, fracJourney);

			if (fracJourney >= 1f) {
				isThrowingPlayer = false;
			}
		}
	}
	
	protected void OnCollisionEnter(Collision col) {
		if (col.gameObject.name == terrain.name && !isHeldByPlayer && wasHeldByPlayer && !isBeingHurt && transform.position.y > minHurtAltitude) {
			StartCoroutine(Hurt(hurtAudioClip.length));
		}
	}
	
	protected IEnumerator Hurt (float length) {
		isBeingHurt = true;
		health -= 1;
		if (!audio.isPlaying) {
			audio.clip = hurtAudioClip;
			audio.Play();
		}
		yield return new WaitForSeconds(length);
		rigidbody.freezeRotation = false;
		StartCoroutine(SitBackUp(5));

	}

	// Boss will return back to their feet after fallen onto ground
	protected IEnumerator SitBackUp (float length) {
		// Transition between fallen on back to standing up
		transform.rotation = Quaternion.Lerp (transform.rotation, Quaternion.Euler(new Vector3 (0, transform.rotation.y, transform.rotation.z)), length);
		yield return new WaitForSeconds(length);
		// Stop ignoring player colliders
		TriggerIgnorePlayerColliders(false);

		agent.enabled = true;
		wasHeldByPlayer = false;
		isBeingHurt = false;
		movement = Movement.Follow;
	}
	
	protected virtual void StartBattle() {
		startedBattle = true;
		rigidbody.useGravity = true;
		animation.Play ("Walk");
		StartCoroutine(StartFollowingPlayer(3));
	}
	
	protected IEnumerator StartFollowingPlayer (float length) {
		agent.speed = 1;
		agent.angularSpeed = 60;
		agent.SetDestination (player.transform.position);
		yield return new WaitForSeconds(length);
		movement = Movement.Follow;

	}
	
	// If player is holding enemy then stop any enemy movement
	protected void IsPlayerHoldingEnemy() {
		if (IsHoldingEnemy ()) {
			movement = Movement.Freeze;
			ChangePlayerSpeed(true);
			rigidbody.useGravity = false;
			rigidbody.freezeRotation = true;
			
			// Ignore player colliders when held by player
			TriggerIgnorePlayerColliders(true);
		} else {
			ChangePlayerSpeed(false);
			if (rigidbody) {
				rigidbody.useGravity = true;
			}
		}
	}

	// Grab player if within grab perimeter
	protected void IsPlayerInGrabPermimeter () {
		float dist = Vector3.Distance(player.transform.position, grabPerimeter.transform.position);

		if (dist < minDistanceGrabPermimeter) {
			StartCoroutine(GrabPlayer ());
		}
	}

	// Boss grabs player
	protected IEnumerator GrabPlayer () {
		Vector3 offset = new Vector3 (-5.0f, 3f, 0f);
		animation.Play (grabAnimationName);
		player.transform.position = transform.position + offset;
		player.transform.forward = transform.forward;
		yield return new WaitForSeconds(animation.clip.length);
		ThrowPlayer ();
	}

	protected void ThrowPlayer () {
		if (isThrowingPlayer) {
			return;
		}
		startMarkerThrowPlayer.transform.position = player.transform.position;
		endMarkerThrowPlayer.transform.position = startMarkerThrowPlayer.transform.position + new Vector3 (-20f, 10f, 0f);
		startTimeThrowPlayer = Time.time;
		journeyLengthThrowPlayer = Vector3.Distance(startMarkerThrowPlayer.transform.position, endMarkerThrowPlayer.transform.position);
		isThrowingPlayer = true;
	}
	
	protected bool IsHoldingEnemy () {
		foreach (HandController playerHandController in playerHandControllers) {
			if (gameObject == playerHandController.GetClosestObject() && playerHandController.IsHoldingObject()) {
				isHeldByPlayer = true;
				wasHeldByPlayer = true;
				animation.Play (heldAnimationName);
				animation[heldAnimationName].speed = heldAnimationSpeed;
				if (heldFixedRotationX >= -360 && heldFixedRotationX <= 360) {
					transform.rotation = Quaternion.Euler(new Vector3 (heldFixedRotationX, transform.rotation.eulerAngles.y, transform.rotation.eulerAngles.z));
				}
				return true;
			}
		}
		
		isHeldByPlayer = false;
		return false;
	}

	protected void ChangePlayerSpeed (bool change) {
		if (change) {
			motor.movement.maxForwardSpeed = playerCarrySpeed;
			motor.movement.maxBackwardsSpeed = playerCarrySpeed;
			playerLook.sensitivityX = playerCarryTurnSpeed;
			playerHydraLook.sensitivityX = playerCarryTurnSpeed;
		} else {
			motor.movement.maxForwardSpeed = playerController.getDefaultMaxForwardSpeed();
			motor.movement.maxBackwardsSpeed = playerController.getDefaultMaxForwardSpeed();
			playerLook.sensitivityX = playerLook.getDefaultSensitivityX();
			playerHydraLook.sensitivityX = playerHydraLook.getDefaultSensitivityX();
		}
	}

	protected virtual void FollowPlayer() {
		agent.SetDestination (player.transform.position);
		agent.speed = followSpeed;
		agent.angularSpeed = followAngularSpeed;
	}
	
	protected virtual void Freeze () {
		agent.enabled = false;
	}

	protected void TriggerIgnorePlayerHandColliders (bool state) {
		foreach (HandController playerHandController in playerHandControllers) {
			Collider[] cols = GetComponentsInChildren<Collider>();
			Collider[] playerHandCols = playerHandController.GetComponentsInChildren<Collider>();
			
			foreach (Collider playerHandCol in playerHandCols) {
				foreach (Collider col in cols) {
					Physics.IgnoreCollision(playerHandCol, col, state);
				}
			}
		}
	}

	protected void TriggerIgnorePlayerColliders (bool state) {
		Collider[] cols = GetComponentsInChildren<Collider>();
		Collider[] playerCols = player.GetComponentsInChildren<Collider>();
		
		foreach (Collider playerCol in playerCols) {
			foreach (Collider col in cols) {
				Physics.IgnoreCollision(playerCol, col, state);
			}
		}
		
		// Always ignore player hand colliders
		if (!state) {
			TriggerIgnorePlayerHandColliders(true);
		}
	}
	
	public void SetInitBattle(bool state) {
		initBattle = state;
	}
}
