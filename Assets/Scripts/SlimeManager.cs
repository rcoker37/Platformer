﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;


public class SlimeManager : MonoBehaviour {

    public Tilemap bridgeActivatorTiles;
    public GameObject slimeObjectPrefab;
    public GameObject slimeObjectIndicatorPrefab;
    public GameObject player;
	public GameObject sprite;
	public GameObject particles;
	public float followDistance;

    public AudioSource SlimeSound;
    public AudioSource WhistleSound;

	private const int NUM_FLOAT_SPRITES = 7;

	private const float FRAME_TIME = 0.1f; //time in seconds per frame of animation
	
	private SpriteRenderer sr;
    private BoxCollider2D bc;

    private SlimeObjectIndicator selectedBridge;
    private GameObject activeBridge;
	private SlimeObjectIndicator lastSelectedBridge;

	private readonly Dictionary<Vector3Int, SlimeObjectIndicator> closeBridges = 
        new Dictionary<Vector3Int, SlimeObjectIndicator>();
    private bool bridgeSwapQueued;

	private Sprite[] floatSprites;
	public Sprite[] bridgeSprites;
	private int animFrame = 0;
	private float frameTime = FRAME_TIME;

	void Start () {
        sr = sprite.GetComponent<SpriteRenderer>();
        bc = GetComponent<BoxCollider2D>();
		LoadFloatSprites();
		LoadBridgeSprites();
		sr.sprite = floatSprites[0];
	}

    private void Awake()
    {
        sr = sprite.GetComponent<SpriteRenderer>();
    }

    private void LoadFloatSprites()
	{
		floatSprites = new Sprite[NUM_FLOAT_SPRITES];
		for (int i = 0; i < NUM_FLOAT_SPRITES; i++)
		{
			floatSprites[i] = Resources.Load<Sprite>("Images/Slime/Float/frame" + (i + 1));
		}
	}

	private void LoadBridgeSprites()
	{
		bridgeSprites = new Sprite[SlimeBridgeAnimation.NUM_FRAMES];
		for (int i = 0; i < SlimeBridgeAnimation.NUM_FRAMES; i++)
		{
			bridgeSprites[i] = Resources.Load<Sprite>("Images/Slime/Bridge/frame" + (i + 1));
		}
	}

	void Update () {
        if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl)
			|| Input.GetKeyDown(KeyCode.JoystickButton1) || Input.GetKeyDown(KeyCode.JoystickButton2))
        {
            bridgeSwapQueued = true;
        }

		frameTime -= Time.deltaTime;
		if (frameTime <= 0)
		{
			frameTime = FRAME_TIME;
			animFrame = (animFrame + 1) % NUM_FLOAT_SPRITES;
			sr.sprite = floatSprites[animFrame];
		}
	}

    private void FixedUpdate()
    {
        bc.offset = player.transform.position - transform.position;
        Vector2 radialVelocity = GetRadialVelocity();
        transform.position += Time.fixedDeltaTime * (new Vector3(radialVelocity.x, radialVelocity.y, 0.0f));

		if (bridgeSwapQueued)
		{
			if (activeBridge != null)
			{
				if (lastSelectedBridge != null)
				{
					lastSelectedBridge.activated = false;
				}

                if (selectedBridge != null &&
					selectedBridge.canActivate &&
					Vector3.Distance(selectedBridge.gameObject.transform.position, activeBridge.transform.position) > 1.0f)
                {
                    // selected bridge is different from active bridge. Immediately switch to selected bridge
                    Destroy(activeBridge.gameObject);
					FormBridge();
                } else
				{
					WhistleSound.Play();
					transform.position = activeBridge.transform.position;
					DestroyBridge();
                }
			}
			else if (selectedBridge != null && selectedBridge.canActivate) 
			{
				SetRender(false);
				particles.SetActive(false);
				FormBridge();
			}
			
			bridgeSwapQueued = false;
		}
    }

	private void FormBridge()
	{
		selectedBridge.activated = true;
		GameObject newSlimeObject = Instantiate(slimeObjectPrefab);
		newSlimeObject.transform.position = selectedBridge.transform.position;
		activeBridge = newSlimeObject;
		SlimeSound.pitch = 1.0f;
		SlimeSound.PlayDelayed(0.5f);
		lastSelectedBridge = selectedBridge;
		newSlimeObject.GetComponentInChildren<SlimeBridgeAnimation>().sprites = bridgeSprites;
	}

	public void DestroyBridge()
	{
        if (activeBridge != null)
        {
		    Destroy(activeBridge.gameObject);
        }
		activeBridge = null;
		SlimeSound.pitch = 0.7f;
		SlimeSound.Play();
		SetRender(true);
		particles.SetActive(true);
	}

	private void SetRender(bool enabled)
	{
		GameObject slimeGuide = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerDemo>().SlimeGuide;
		
		SpriteRenderer renderer = slimeGuide.activeSelf ? slimeGuide.GetComponent<SpriteRenderer>() : sr;
		renderer.enabled = enabled;
	}

    private Vector2 GetRadialVelocity()
    {
        Vector2 parentPos = new Vector2(player.transform.position.x, player.transform.position.y);
        Vector2 currentPos = new Vector2(transform.position.x, transform.position.y);

        Vector2 playerVel = player.GetComponent<Rigidbody2D>().velocity;
        Vector2 playerDirection = player.GetComponent<PlayerDemo>().GetFacing() * Vector2.left;
        Vector2 followPos = parentPos + -1.0f * followDistance * playerDirection;

        Vector2 targetVel = 3.0f * (followPos - currentPos) + 0.5f * playerVel;
        return targetVel;
    }

    private Vector2 GetRotatedPosition(float newAngle, Vector2 parentPos, Vector2 currentPos)
    {
        float dist = Vector2.Distance(parentPos, currentPos);
        return new Vector2(parentPos.x + dist * Mathf.Cos(newAngle), parentPos.y + dist * Mathf.Sin(newAngle));
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        HashSet<Vector3Int> contactCells = new HashSet<Vector3Int>();
        foreach (ContactPoint2D contact in collision.contacts)
        {
            Vector3 centerTilePosition = GetConnectedTileCenter(contact.point);
            Vector3Int centerTileCell = bridgeActivatorTiles.WorldToCell(centerTilePosition);

            if (!closeBridges.ContainsKey(centerTileCell))
            {
                GameObject newSlime = Instantiate(slimeObjectIndicatorPrefab);
                newSlime.transform.position = centerTilePosition;

                closeBridges[centerTileCell] = newSlime.GetComponent<SlimeObjectIndicator>();
            }

            contactCells.Add(centerTileCell);
        }

        HashSet<Vector3Int> keys = new HashSet<Vector3Int>(closeBridges.Keys);
        foreach (Vector3Int contactCell in keys)
        {
            if (!contactCells.Contains(contactCell))
            {
                closeBridges[contactCell].hint = false;
                closeBridges.Remove(contactCell);
            }

        }

        UpdateClosestSlimeObject();
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        HashSet<Vector3Int> keys = new HashSet<Vector3Int>(closeBridges.Keys);
        foreach (Vector3Int contactCell in keys)
        {
            closeBridges[contactCell].hint = false;
            closeBridges.Remove(contactCell);
        }

        UpdateClosestSlimeObject();
    }

    private void UpdateClosestSlimeObject()
    {
        float minDist = float.MaxValue;
        SlimeObjectIndicator closestBridge = null;
        foreach (Vector3Int cellPos in closeBridges.Keys)
        {
            float dist = Vector3.Distance(bridgeActivatorTiles.CellToWorld(cellPos), player.transform.position); 
            if (dist < minDist)
            {
                minDist = dist;
                closestBridge = closeBridges[cellPos];
            }
        }

        if (closestBridge != null)
        {
            foreach (SlimeObjectIndicator bridge in closeBridges.Values)
            {
                bridge.hint = bridge == closestBridge;
            }
        }

        selectedBridge = closestBridge;
    }

    private Vector3 GetConnectedTileCenter(Vector2 worldPosition)
    {
        HashSet<Vector3> connectedTilePositions = new HashSet<Vector3>();

        Vector3Int contactCellPosition = bridgeActivatorTiles.GetComponent<Tilemap>().WorldToCell(worldPosition);

        for (int yDist = -1; yDist <= 1; yDist++)
        {
            for (int xDist = -1; xDist <= 1; xDist++)
            {
                Vector3Int centerCellPosition = contactCellPosition + new Vector3Int(0, yDist, 0);
                if (bridgeActivatorTiles.HasTile(centerCellPosition))
                {
                    connectedTilePositions.Add(bridgeActivatorTiles.GetCellCenterWorld(centerCellPosition));
                }

                for (int xDistLeft = 1; true; xDistLeft++)
                {
                    Vector3Int cellPosition = centerCellPosition + new Vector3Int(-1 * xDistLeft, 0, 0);
                    if (!bridgeActivatorTiles.HasTile(cellPosition)) {
                        break;
                    }

                    connectedTilePositions.Add(bridgeActivatorTiles.GetCellCenterWorld(cellPosition));
                }

                for (int xDistRight = 1; true; xDistRight++)
                {
                    Vector3Int cellPosition = centerCellPosition + new Vector3Int(xDistRight, 0, 0);
                    if (!bridgeActivatorTiles.HasTile(cellPosition)) {
                        break;
                    }

                    connectedTilePositions.Add(bridgeActivatorTiles.GetCellCenterWorld(cellPosition));
                }
            }
        }

        Vector3 avgPos = Vector3.zero;

        foreach (Vector3 tilePos in connectedTilePositions)
        {
            avgPos += tilePos;
        }

        return (1.0f / connectedTilePositions.Count) * avgPos;
    }

	public bool CanActivateBridge()
	{
		return selectedBridge != null && selectedBridge.canActivate;
	}
}
