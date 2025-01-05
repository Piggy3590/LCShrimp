using System.Collections;
using System.Collections.Generic;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations.Rigging;

namespace Shrimp.Patches
{
    public class ShrimpEnemyAI : EnemyAI
    {
        public AISearchRoutine searchForItems;

        public AISearchRoutine searchForPlayer;

        public float hungerValue;
        public Transform mouthTransform;
        private Vector3 mouthOriginalScale;
        public AudioSource growlAudio;
        public AudioSource dogRageAudio;
        public AudioSource hungerAudio;
        public AudioSource sprintAudio;
        public bool inKillAnimation;
        public bool startingKillAnimationLocalClient;
        private float scaredBackingAway;
        
	    private Ray backAwayRay;
        private RaycastHit hitInfo;
        private RaycastHit hitInfoB;
        private PlayerControllerB lastHitPlayer;

        /*
        [Header("Tracking/Memory")]
        [Space(3f)]
        public Vector3 nestPosition;

        private bool choseNestPosition;
        */

        [Space(3f)]
        public float angryTimer;
        public GrabbableObject targetItem;
        public HoarderBugItem heldItem;
        private Light lungLight;

        [Header("Animations")]
        [Space(5f)]
        private Transform rightEye;
        private Transform leftEye;
        private Vector3 scaleOfEyesNormally;
        private Vector3 agentLocalVelocity;
        private Vector3 previousPosition;
        private float velX;
        private float velZ;
        public Transform turnCompass;
        private float armsHoldLayerWeight;

        [Space(5f)]
        public Transform animationContainer;
        public Transform grabTarget;
        public TwoBoneIKConstraint headLookRig;
        public Transform headLookTarget;

        [Header("Special behaviour states")]
        private float annoyanceMeter;
        public bool watchingPlayerNearPosition;
        public PlayerControllerB watchingPlayer;
        public Transform lookTarget;
        public bool lookingAtPositionOfInterest;
        private Vector3 positionOfInterest;

        private bool isAngry;

        [Header("Misc logic")]
        private bool sendingGrabOrDropRPC;
        private float waitingAtNestTimer;
        private bool waitingAtNest;
        private float timeSinceSeeingAPlayer;

        [Header("Chase logic")]
        private bool lostPlayerInChase;
        private float noticePlayerTimer;
        public PlayerControllerB angryAtPlayer;
        private bool inChase;

        [Header("Audios")]
        public AudioClip[] chitterSFX;

        [Header("Audios")]
        public AudioClip[] angryScreechSFX;

        public AudioClip angryVoiceSFX;

        public AudioClip bugFlySFX;

        public AudioClip hitPlayerSFX;

        private float timeSinceHittingPlayer;

        private float timeSinceLookingTowardsNoise;
        private float timeSinceLookingTowardsItem;

        private float detectPlayersInterval;

        private bool inReturnToNestMode;

        private float footStepTime;

        [ServerRpc(RequireOwnership = false)]
        void SyncHungerValueServerRpc(float value)
        {
            SyncHungerValueClientRpc(value);
        }

        [ClientRpc]
        void SyncHungerValueClientRpc(float value)
        {
            hungerValue = value;
        }

        void SetVariables()
        {
            agent = this.GetComponent<NavMeshAgent>();
            eye = transform.GetChild(0).GetChild(1).GetChild(0).GetChild(0).GetChild(0).GetChild(0).GetChild(2).GetChild(0).GetChild(0).GetChild(0).GetChild(1);
            leftEye = transform.GetChild(0).GetChild(1).GetChild(0).GetChild(0).GetChild(0).GetChild(0).GetChild(2).GetChild(0).GetChild(0).GetChild(0).GetChild(2);
            rightEye = transform.GetChild(0).GetChild(1).GetChild(0).GetChild(0).GetChild(0).GetChild(0).GetChild(2).GetChild(0).GetChild(0).GetChild(0).GetChild(3);
            scaleOfEyesNormally = leftEye.localScale;
            headLookRig = transform.GetChild(0).GetChild(1).GetChild(2).GetChild(0).GetComponent<TwoBoneIKConstraint>();
            animationContainer = transform.GetChild(0);
            grabTarget = transform.GetChild(0).GetChild(1).GetChild(0).GetChild(0).GetChild(0).GetChild(0).GetChild(2).GetChild(0).GetChild(0).GetChild(0).GetChild(5);
            headLookTarget = transform.GetChild(0).GetChild(1).GetChild(2).GetChild(0).GetChild(0);
            lookTarget = transform.GetChild(2);
            turnCompass = transform.GetChild(3);
            lungLight = GameObject.Find("LungFlash").GetComponent<Light>();
            lungLight.intensity = 0;
            List <EnemyBehaviourState>enemyBehaviourStatesList = new List<EnemyBehaviourState>();

            EnemyBehaviourState enemyBehaviourState1 = new EnemyBehaviourState();
            enemyBehaviourState1.name = "Roaming";
            enemyBehaviourStatesList.Add(enemyBehaviourState1);
            EnemyBehaviourState enemyBehaviourState2 = new EnemyBehaviourState();
            enemyBehaviourState2.name = "Following";
            enemyBehaviourStatesList.Add(enemyBehaviourState2);
            EnemyBehaviourState enemyBehaviourState3 = new EnemyBehaviourState();
            enemyBehaviourState3.name = "Chasing";
            enemyBehaviourStatesList.Add(enemyBehaviourState3);

            enemyBehaviourStates = enemyBehaviourStatesList.ToArray();
        }

        public override void Start()
        {
            SetVariables();
            base.Start();

            lastHitPlayer = StartOfRound.Instance.allPlayerScripts[0];
            dogRageAudio.volume = 0;
            dogRageAudio.loop = true;
            dogRageAudio.clip = Plugin.enragedScream;
            dogRageAudio.pitch = 0f;
            dogRageAudio.Play();

            growlAudio.volume = 0;
            growlAudio.loop = true;
            growlAudio.clip = Plugin.bigGrowl;
            growlAudio.Play();

            hungerAudio.volume = 0;
            hungerAudio.loop = true;
            hungerAudio.clip = Plugin.stomachGrowl;
            hungerAudio.Play();

            sprintAudio.volume = 0;
            sprintAudio.loop = true;
            sprintAudio.clip = Plugin.dogSprint;
            sprintAudio.Play();

            mouthOriginalScale = mouthTransform.localScale;
            heldItem = null;
            creatureAnimator.SetTrigger("Walk");
            lungLight.intensity = 0;
        }

        private bool EatTargetItemIfClose()
        {
            if (targetItem != null && heldItem == null && Vector3.Distance(base.transform.position, targetItem.transform.position) < 0.75f && !targetItem.deactivated)
            {
                NetworkObject component = targetItem.GetComponent<NetworkObject>();
                EatItemServerRpc(component);
                return true;
            }
            return false;
        }

        public override void DoAIInterval()
        {
            base.DoAIInterval();
            if (isEnemyDead || StartOfRound.Instance.allPlayersDead)
            {
                return;
            }
            CalculateAnimationDirection();

            if (currentBehaviourStateIndex != 2 &&scaredBackingAway > 0.003f && hungerValue < 40)
            {
                Vector3 position = lastHitPlayer.transform.position;
                position.y = transform.position.y;
                Vector3 vector = position - transform.position;
                backAwayRay = new Ray(transform.position, vector * -1f);
                if (Physics.Raycast(backAwayRay, out hitInfo, 60f, StartOfRound.Instance.collidersAndRoomMaskAndDefault))
                {
                    if (hitInfo.distance < 4f)
                    {
                        if (Physics.Linecast(transform.position, hitInfo.point + Vector3.Cross(vector, Vector3.up) * 25.5f, out hitInfoB, StartOfRound.Instance.collidersAndRoomMaskAndDefault))
                        {
                            float distance = hitInfoB.distance;
                            if (Physics.Linecast(transform.position, hitInfo.point + Vector3.Cross(vector, Vector3.up) * -25.5f, out hitInfoB, StartOfRound.Instance.collidersAndRoomMaskAndDefault))
                            {
                                float distance2 = hitInfoB.distance;
                                if (Mathf.Abs(distance - distance2) < 5f)
                                {
                                    agent.destination = hitInfo.point + Vector3.Cross(vector, Vector3.up) * -4.5f;
                                }
                                else if (distance < distance2)
                                {
                                    agent.destination = hitInfo.point + Vector3.Cross(vector, Vector3.up) * -4.5f;
                                }
                                else
                                {
                                    agent.destination = hitInfo.point + Vector3.Cross(vector, Vector3.up) * 4.5f;
                                }
                            }
                        }
                    }
                    else
                    {
                        agent.destination = hitInfo.point;
                    }
                }
                agent.stoppingDistance = 0f;
                Quaternion quaternion = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(vector), 3f * Time.deltaTime);
                transform.eulerAngles = new Vector3(0f, quaternion.eulerAngles.y, 0f);
                agent.speed = 13f;
                creatureAnimator.SetFloat("walkSpeed", -3.5f);
                return;
            }

            switch (currentBehaviourStateIndex)
            {
                case 0:
                    {
                        if (inKillAnimation)
                        {
                            return;
                        }
                        //ExitChaseMode();
                        movingTowardsTargetPlayer = false;
                        if (!searchForPlayer.inProgress)
                        {
                            StartSearch(transform.position, searchForPlayer);
                            break;
                        }
                        if (hungerValue > 0 && (CheckLineOfSightForPlayer(65f, 80, -1) || targetPlayer != null))
                        {
                            targetPlayer = CheckLineOfSightForPlayer(65f, 80, -1);
                            SwitchToBehaviourState(1);
                        }
                        break;
                    }
                case 1:
                    //ExitChaseMode();
                    if (inKillAnimation)
                    {
                        return;
                    }
                    StopSearch(searchForPlayer);
                    if (hungerValue <= 0)
                    {
                        SwitchToBehaviourState(0);
                    }
                    if (targetItem != null && heldItem == null && Vector3.Distance(base.transform.position, targetItem.transform.position) < 0.75f && !targetItem.deactivated)
                    {
                        if (ShrimpItemManager.Instance.droppedItems.Count > 0)
                        {
                            GameObject gameObject2 = CheckLineOfSight(ShrimpItemManager.Instance.droppedObjects, 60f, 40, 5f);
                            if ((bool)gameObject2)
                            {
                                GrabbableObject component = gameObject2.GetComponent<GrabbableObject>();
                                if ((bool)component && !component.isHeld && !component.isPocketed && !component.deactivated)
                                {
                                    SetGoTowardsTargetObject(gameObject2);
                                }
                            }
                        }
                        movingTowardsTargetPlayer = false;
                    }else
                    {
                        if ((targetPlayer != null && Vector3.Distance(base.transform.position, targetPlayer.transform.position) < 2.5f)
                            || (watchingPlayer != null && Vector3.Distance(base.transform.position, watchingPlayer.transform.position) < 2.5f))
                        {
                            agent.stoppingDistance = 4.5f;
                            movingTowardsTargetPlayer = false;
                            BackAway();
                        }else if (CheckLineOfSight(ShrimpItemManager.Instance.droppedObjects, 160f, 40, 5f) != null)
                        {
                            agent.stoppingDistance = 0;
                            movingTowardsTargetPlayer = false;
                            if (ShrimpItemManager.Instance.droppedItems.Count > 0)
                            {
                                GameObject gameObject2 = CheckLineOfSight(ShrimpItemManager.Instance.droppedObjects, 160f, 40, 5f);
                                if ((bool)gameObject2)
                                {
                                    GrabbableObject component = gameObject2.GetComponent<GrabbableObject>();
                                    if ((bool)component && !component.isHeld && !component.isPocketed && !component.deactivated)
                                    {
                                        SetGoTowardsTargetObject(gameObject2);
                                    }
                                }
                            }
                        }else
                        {
                            agent.stoppingDistance = 4.5f;
                            movingTowardsTargetPlayer = true;
                        }
                    }
                    PlayerControllerB playerControllerB2 = base.CheckLineOfSightForPlayer(65f, 80, -1);

                    if (playerControllerB2 != null)
                    {
                        noticePlayerTimer = 0;
                    }else
                    {
                        noticePlayerTimer += 0.075f;
                        if (noticePlayerTimer > 3f)
                        {
                            lostPlayerInChase = true;
                        }
                    }
                    if (lostPlayerInChase && targetItem == null)
                    {
                        SwitchToBehaviourState(0);
                        break;
                    }
                    break;
                case 2:
                    if (inKillAnimation)
                    {
                        return;
                    }
                    inReturnToNestMode = false;
                    if (heldItem != null)
                    {
                        //DropItemAndCallDropRPC(heldItem.itemGrabbableObject.GetComponent<NetworkObject>(), droppedInNest: false);
                    }
                    if (lostPlayerInChase)
                    {
                        if (!searchForPlayer.inProgress)
                        {
                            searchForPlayer.searchWidth = 30f;
                            StartSearch(targetPlayer.transform.position, searchForPlayer);
                            Debug.Log(base.gameObject.name + ": Lost player in chase; beginning search where the player was last seen");
                        }
                        break;
                    }
                    if (CheckLineOfSight(ShrimpItemManager.Instance.droppedObjects, 160f, 40, 5f) != null)
                    {
                        agent.stoppingDistance = 0;
                        movingTowardsTargetPlayer = false;
                        if (ShrimpItemManager.Instance.droppedItems.Count > 0)
                        {
                            GameObject gameObject2 = CheckLineOfSight(ShrimpItemManager.Instance.droppedObjects, 160f, 40, 5f);
                            if ((bool)gameObject2)
                            {
                                GrabbableObject component = gameObject2.GetComponent<GrabbableObject>();
                                if ((bool)component && !component.isHeld && !component.isPocketed && !component.deactivated)
                                {
                                    SetGoTowardsTargetObject(gameObject2);
                                }
                            }
                        }
                    }
                    if (hungerValue < 40)
                    {
                        SwitchToBehaviourState(0);
                    }
                    if (targetPlayer == null)
                    {
                        Debug.LogError("TargetPlayer is null even though bug is in chase; setting targetPlayer to watchingPlayer");
                        if (watchingPlayer != null)
                        {
                            targetPlayer = watchingPlayer;
                        }
                    }
                    if (searchForPlayer.inProgress)
                    {
                        StopSearch(searchForPlayer);
                        Debug.Log(base.gameObject.name + ": Found player during chase; stopping search coroutine and moving after target player");
                    }
                    break;
                case 3:
                    break;
            }
        }

        private void SetGoTowardsTargetObject(GameObject foundObject)
        {
            agent.stoppingDistance = 0;
            if (SetDestinationToPosition(foundObject.transform.position, checkForPath: true) && ShrimpItemManager.Instance.droppedObjects.Contains(foundObject))
            {
                Debug.Log(base.gameObject.name + ": Setting target object and going towards it.");
                targetItem = foundObject.GetComponent<GrabbableObject>();
                EatTargetItemIfClose();
                StopSearch(searchForItems, clear: false);
            }
            else
            {
                targetItem = null;
                Debug.Log(base.gameObject.name + ": i found an object but cannot reach it (or it has been taken by another bug): " + foundObject.name);
            }
        }

        private void ExitChaseMode()
        {
            if (inChase)
            {
                inChase = false;
                Debug.Log(base.gameObject.name + ": Exiting chase mode");
                if (searchForPlayer.inProgress)
                {
                    StopSearch(searchForPlayer);
                }
                movingTowardsTargetPlayer = false;
                creatureAnimator.SetBool("Chase", value: false);
                creatureSFX.Stop();
            }
        }
        /*
        private void SetReturningToNest()
        {
            if (SetDestinationToPosition(nestPosition, checkForPath: true))
            {
                targetItem = null;
                StopSearch(searchForItems, clear: false);
            }
            else
            {
                Debug.Log(base.gameObject.name + ": Return to nest was called, but nest is not accessible! Abandoning and choosing a new nest position.");
                ChooseNestPosition();
            }
        }
        */

        private void LateUpdate()
        {
            if (currentBehaviourStateIndex != 2 && scaredBackingAway > 0.003f && hungerValue < 40)
            {
                scaredBackingAway -= Time.deltaTime;
            }
            if (!inSpecialAnimation && !isEnemyDead && !StartOfRound.Instance.allPlayersDead)
            {
                if (detectPlayersInterval <= 0f)
                {
                    detectPlayersInterval = 0.2f;
                    DetectAndLookAtPlayers();
                }
                else
                {
                    detectPlayersInterval -= Time.deltaTime;
                }
                AnimateLooking();
                /*
                SetArmLayerWeight();
                */
            }
        }


        void CalculateAnimationDirection(float maxSpeed = 1f)
        {
            agentLocalVelocity = animationContainer.InverseTransformDirection(Vector3.ClampMagnitude(transform.position - previousPosition, 1f) / (Time.deltaTime * 4f));
            creatureAnimator.SetFloat("walkSpeed", Mathf.Clamp(agentLocalVelocity.magnitude / 5f, 0f, 3f));
            creatureAnimator.SetFloat("runSpeed", Mathf.Clamp(agentLocalVelocity.magnitude / 2.7f, 3f, 4f));
            CalculateAnimationDirectionServerRpc(agentLocalVelocity, creatureAnimator.GetFloat("walkSpeed"), creatureAnimator.GetFloat("runSpeed"));
        }

        [ServerRpc]
        private void CalculateAnimationDirectionServerRpc(Vector3 localVel, float walkSp, float runSp)
        {
            CalculateAnimationDirectionClientRpc(localVel, walkSp, runSp);
        }

        [ClientRpc]
        void CalculateAnimationDirectionClientRpc(Vector3 localVel, float walkSp, float runSp)
        {
            agentLocalVelocity = localVel;
            previousPosition = transform.position;
            creatureAnimator.SetFloat("walkSpeed", walkSp);
            creatureAnimator.SetFloat("runSpeed", runSp);
        }

        private void AnimateLooking()
        {
            if (hungerValue < 47)
            {
                agent.angularSpeed = 100f;
            }else
            {
                agent.angularSpeed = 1000f;
            }
            if (targetItem != null)
            {
                lookTarget.position = targetItem.transform.position;
                lookingAtPositionOfInterest = true;
                if (targetItem.isHeld)
                {
                    if (targetPlayer != null)
                    {
                        lookTarget.position = targetPlayer.transform.position;
                    }else
                    {
                        lookingAtPositionOfInterest = false;
                    }
                    targetItem = null;
                }
            }
            if (lookTarget != null && !lookingAtPositionOfInterest && watchingPlayer != null)
            {
                lookTarget.position = watchingPlayer.gameplayCamera.transform.position;
            }
            else
            {
                if (!lookingAtPositionOfInterest)
                {
                    headLookRig.weight = Mathf.Lerp(headLookRig.weight, 0f, 10f);
                    return;
                }
            }
            if (base.IsOwner)
            {
                /*
                turnCompass.LookAt(lookTarget);
                base.transform.rotation = Quaternion.Lerp(base.transform.rotation, turnCompass.rotation, 6f * Time.deltaTime);
                base.transform.localEulerAngles = new Vector3(0f, base.transform.localEulerAngles.y, 0f);
                */
            }
            if (watchingPlayer != null && !lookingAtPositionOfInterest)
            {
                float num = Vector3.Angle(base.transform.forward, lookTarget.position - base.transform.position);
                Vector3 vector = watchingPlayer.transform.position - base.transform.position;
                if (num > 22f)
                {
                    Quaternion quaternion = Quaternion.Slerp(base.transform.rotation, Quaternion.LookRotation(vector), 3f * Time.deltaTime);
                    base.transform.eulerAngles = new Vector3(0f, quaternion.eulerAngles.y, 0f);
                }
            }else
            {
                lookTarget.position = positionOfInterest;
            }
            headLookRig.weight = Mathf.Lerp(headLookRig.weight, 0.5f, 7f);
            headLookTarget.position = Vector3.Lerp(headLookTarget.position, lookTarget.position, 8f * Time.deltaTime);
        }

        private void DetectAndLookAtPlayers()
        {
            Vector3 b = base.transform.position;
            PlayerControllerB[] allPlayersInLineOfSight = GetAllPlayersInLineOfSight(70f, 30, eye, 1.2f);
            if (allPlayersInLineOfSight != null)
            {
                PlayerControllerB playerControllerB = watchingPlayer;
                timeSinceSeeingAPlayer = 0f;
                float num = 500f;
                bool flag = false;
                if (stunnedByPlayer != null)
                {
                    flag = true;
                    angryAtPlayer = stunnedByPlayer;
                }
                for (int i = 0; i < allPlayersInLineOfSight.Length; i++)
                {
                    if (IsHoarderBugAngry() && allPlayersInLineOfSight[i] == angryAtPlayer)
                    {
                        watchingPlayer = angryAtPlayer;
                    }
                    else
                    {
                        float num2 = Vector3.Distance(allPlayersInLineOfSight[i].transform.position, b);
                        if (num2 < num)
                        {
                            num = num2;
                            watchingPlayer = allPlayersInLineOfSight[i];
                        }
                    }
                    float num3 = Vector3.Distance(allPlayersInLineOfSight[i].transform.position, base.transform.position);
                    if (ShrimpItemManager.Instance.droppedItems.Count > 0)
                    {
                        if ((num3 < 4f || (inChase && num3 < 8f)) && angryTimer < 3.25f)
                        {
                            angryAtPlayer = allPlayersInLineOfSight[i];
                            watchingPlayer = allPlayersInLineOfSight[i];
                            angryTimer = 3.25f;
                            break;
                        }
                        /*
                        if (!isAngry && currentBehaviourStateIndex == 0 && num3 < 8f && (targetItem == null || Vector3.Distance(targetItem.transform.position, base.transform.position) > 7.5f) && base.IsOwner)
                        {
                            SwitchToBehaviourState(1);
                        }
                        */
                    }
                    if (currentBehaviourStateIndex != 2 && Vector3.Distance(base.transform.position, allPlayersInLineOfSight[i].transform.position) < 2.5f)
                    {
                        annoyanceMeter += 0.2f;
                        if (annoyanceMeter > 2.5f)
                        {
                            angryAtPlayer = allPlayersInLineOfSight[i];
                            watchingPlayer = allPlayersInLineOfSight[i];
                            angryTimer = 3.25f;
                        }
                    }
                }
                watchingPlayerNearPosition = num < 6f;
                if (watchingPlayer != playerControllerB)
                {
                    RoundManager.PlayRandomClip(creatureVoice, chitterSFX);
                }
                if (!base.IsOwner)
                {
                    return;
                }
                if (currentBehaviourStateIndex != 2)
                {
                    if (IsHoarderBugAngry())
                    {
                        lostPlayerInChase = false;
                        targetPlayer = watchingPlayer;
                        //SwitchToBehaviourState(2);
                    }
                }
                else
                {
                    targetPlayer = watchingPlayer;
                    if (lostPlayerInChase)
                    {
                        lostPlayerInChase = false;
                    }
                }
                return;
            }
            timeSinceSeeingAPlayer += 0.2f;
            watchingPlayerNearPosition = false;
            if (currentBehaviourStateIndex != 2)
            {
                if (timeSinceSeeingAPlayer > 1.5f)
                {
                    watchingPlayer = null;
                }
                return;
            }
            if (timeSinceSeeingAPlayer > 1.25f)
            {
                watchingPlayer = null;
            }
            if (base.IsOwner)
            {
                if (timeSinceSeeingAPlayer > 15f)
                {
                    //SwitchToBehaviourState(1);
                }
                else if (timeSinceSeeingAPlayer > 2.5f)
                {
                    lostPlayerInChase = true;
                }
            }
        }

        private bool IsHoarderBugAngry()
        {
            if (stunNormalizedTimer > 0f)
            {
                angryTimer = 4f;
                if ((bool)stunnedByPlayer)
                {
                    angryAtPlayer = stunnedByPlayer;
                }
                return true;
            }
            int num = 0;
            int num2 = 0;
            if (!(angryTimer > 0f))
            {
                return num2 > 0;
            }
            return true;
        }

        public override void Update()
        {
            base.Update();

            footStepTime += Time.deltaTime * agentLocalVelocity.magnitude / 8f;
            if (footStepTime > 0.5f)
            {
                creatureVoice.PlayOneShot(Plugin.footsteps[Random.Range(0, 5)], Random.Range(0.8f, 1f));
                footStepTime = 0f;
            }

            Plugin.mls.LogInfo($"hungerValue: {hungerValue}, currentBehaviourState: {currentBehaviourStateIndex}");
            timeSinceHittingPlayer += Time.deltaTime;
            timeSinceLookingTowardsNoise += Time.deltaTime;
            if (timeSinceLookingTowardsNoise > 0.6f && !CheckLineOfSightForItem(120, 40, 3))
            {
                lookingAtPositionOfInterest = false;
            }
            if (inSpecialAnimation || isEnemyDead || StartOfRound.Instance.allPlayersDead)
            {
                return;
            }
            if ((targetPlayer != null || hungerValue <= 0) && IsOwner)
            {
                SyncHungerValueServerRpc(hungerValue += Time.deltaTime);
            }
            if (hungerValue > 34)
            {
                hungerAudio.volume = Mathf.Lerp(hungerAudio.volume, 1f, Time.deltaTime * 2);

            }
            else
            {
                hungerAudio.volume = Mathf.Lerp(hungerAudio.volume, 0, Time.deltaTime * 2);
            }
            if (hungerValue > 40)
            {
                leftEye.localScale = Vector3.Lerp(leftEye.localScale, scaleOfEyesNormally * 0.4f, 20f * Time.deltaTime);
                rightEye.localScale = Vector3.Lerp(rightEye.localScale, scaleOfEyesNormally * 0.4f, 20f * Time.deltaTime);
                growlAudio.volume = 1;
            }
            else
            {
                leftEye.localScale = Vector3.Lerp(leftEye.localScale, scaleOfEyesNormally, 20f * Time.deltaTime);
                rightEye.localScale = Vector3.Lerp(rightEye.localScale, scaleOfEyesNormally, 20f * Time.deltaTime);
                growlAudio.volume = Mathf.Lerp(growlAudio.volume, 0, Time.deltaTime * 5);
            }
            if (hungerValue > 45)
            {
                dogRageAudio.volume = 1f;
                dogRageAudio.pitch = 0.8f;
                mouthTransform.localScale = Vector3.Lerp(mouthTransform.localScale, new Vector3(0.005590725f, 0.01034348f, 0.02495567f), 30f * Time.deltaTime);
            }else
            {
                dogRageAudio.volume = Mathf.Lerp(dogRageAudio.volume, 0, Time.deltaTime * 10);
                dogRageAudio.pitch = Mathf.Lerp(dogRageAudio.pitch, 0f, Time.deltaTime * 10);
                mouthTransform.localScale = Vector3.Lerp(mouthTransform.localScale, mouthOriginalScale, 20f * Time.deltaTime);
            }
            if (hungerValue > 48)
            {
                sprintAudio.volume = Mathf.Lerp(sprintAudio.volume, 1f, Time.deltaTime * 10);
                creatureAnimator.SetBool("running", true);
                if (currentBehaviourStateIndex != 2)
                {
                    SwitchToBehaviourState(2);
                }
            }else
            {
                sprintAudio.volume = Mathf.Lerp(sprintAudio.volume, 0f, Time.deltaTime * 10);
                creatureAnimator.SetBool("running", false);
                if (currentBehaviourStateIndex == 2)
                {
                    SwitchToBehaviourState(0);
                }
            }
            creatureAnimator.SetBool("stunned", stunNormalizedTimer > 0f);
            bool flag = IsHoarderBugAngry();
            if (!isAngry && flag)
            {
                isAngry = true;
                creatureVoice.clip = angryVoiceSFX;
                creatureVoice.Play();
            }
            else if (isAngry && !flag)
            {
                isAngry = false;
                angryAtPlayer = null;
                creatureVoice.Stop();
            }

            switch (currentBehaviourStateIndex)
            {
                case 0:
                    //searching players
                    if (inKillAnimation) return;
                    if (CheckLineOfSightForItem(160, 40, 3))
                    {
                        timeSinceLookingTowardsItem = 0;
                        positionOfInterest = CheckLineOfSightForItem(160, 40, 3).transform.position;
                        lookingAtPositionOfInterest = true;
                    }
                    else if (timeSinceLookingTowardsItem < 0.6f)
                    {
                        timeSinceLookingTowardsItem += Time.deltaTime;
                    }

                    if (!searchForPlayer.inProgress)
                    {
                        StartSearch(base.transform.position, searchForPlayer);
                        break;
                    }
                    addPlayerVelocityToDestination = 0f;


                    if (stunNormalizedTimer > 0f)
                    {
                        agent.speed = 0f;
                    }
                    else
                    {
                        agent.speed = 6f;
                    }
                    break;
                case 1:
                    //following player
                    //ExitChaseMode();
                    if (inKillAnimation) return;

                    if (CheckLineOfSightForItem(160, 40, 3))
                    {
                        timeSinceLookingTowardsItem = 0;
                        positionOfInterest = CheckLineOfSightForItem(160, 40, 3).transform.position;
                        lookingAtPositionOfInterest = true;
                    }
                    else if (timeSinceLookingTowardsItem < 0.6f)
                    {
                        timeSinceLookingTowardsItem += Time.deltaTime;
                    }

                    addPlayerVelocityToDestination = 0f;
                    if (stunNormalizedTimer > 0f)
                    {
                        agent.speed = 0f;
                    }
                    else
                    {
                        agent.speed = 6f;
                    }
                    agent.acceleration = 30f;
                    break;
                case 2:
                    //chasing player
                    //isHeldBefore
                    if (inKillAnimation) return;
                    agent.stoppingDistance = 0;
                    if (!inChase)
                    {
                        inChase = true;
                        if (GameNetworkManager.Instance.localPlayerController.HasLineOfSightToPosition(base.transform.position + Vector3.up * 0.75f, 60f, 15))
                        {
                            GameNetworkManager.Instance.localPlayerController.JumpToFearLevel(0.7f, true);
                        }
                    }
                    addPlayerVelocityToDestination = 2f;
                    if (!base.IsOwner)
                    {
                        break;
                    }
                    
                    /*
                    if (!targetPlayer && currentBehaviourStateIndex != 0)
                    {
                        SwitchToBehaviourState(0);
                    }
                    */

                    if (stunNormalizedTimer > 0f)
                    {
                        agent.speed = 0f;
                    }
                    else
                    {
                        agent.speed = 18f;
                    }
                    agent.acceleration = 100f;
                    break;
            }
        }

        public override void DetectNoise(Vector3 noisePosition, float noiseLoudness, int timesPlayedInOneSpot = 0, int noiseID = 0)
        {
            base.DetectNoise(noisePosition, noiseLoudness, timesPlayedInOneSpot, noiseID);
            if (currentBehaviourStateIndex == 0 && timesPlayedInOneSpot <= 10 && !(timeSinceLookingTowardsNoise < 0.6f))
            {
                timeSinceLookingTowardsNoise = 0f;
                float num = Vector3.Distance(noisePosition, base.transform.position);
                positionOfInterest = noisePosition;
                lookingAtPositionOfInterest = true;
            }
        }

        private void DropItemAndCallDropRPC(NetworkObject dropItemNetworkObject, bool droppedInNest = true)
        {
            Vector3 targetFloorPosition = RoundManager.Instance.RandomlyOffsetPosition(heldItem.itemGrabbableObject.GetItemFloorPosition(), 1.2f, 0.4f);
            DropItem(dropItemNetworkObject, targetFloorPosition);
            sendingGrabOrDropRPC = true;
            DropItemServerRpc(dropItemNetworkObject, targetFloorPosition, droppedInNest);
        }

        [ServerRpc]
        public void DropItemServerRpc(NetworkObjectReference objectRef, Vector3 targetFloorPosition, bool droppedInNest)
        {
            {
                DropItemClientRpc(objectRef, targetFloorPosition, droppedInNest);
            }
        }
        [ClientRpc]
        public void DropItemClientRpc(NetworkObjectReference objectRef, Vector3 targetFloorPosition, bool droppedInNest)
        {
            {
                if (objectRef.TryGet(out var networkObject))
                {
                    DropItem(networkObject, targetFloorPosition, droppedInNest);
                }
                else
                {
                    Debug.LogError(base.gameObject.name + ": Failed to get network object from network object reference (Drop item RPC)");
                }
            }
        }
        [ServerRpc]
        public void EatItemServerRpc(NetworkObjectReference objectRef)
        {
            {
                EatItemClientRpc(objectRef);
            }
        }
        [ClientRpc]
        public void EatItemClientRpc(NetworkObjectReference objectRef)
        {
            {
                if (objectRef.TryGet(out var networkObject))
                {
                    EatItem(networkObject);
                }
                else
                {
                    Debug.LogError(base.gameObject.name + ": Failed to get network object from network object reference (Grab item RPC)");
                }
            }
        }
        private void DropItem(NetworkObject item, Vector3 targetFloorPosition, bool droppingInNest = true)
        {
            if (sendingGrabOrDropRPC)
            {
                sendingGrabOrDropRPC = false;
                return;
            }
            if (heldItem == null)
            {
                Debug.LogError("Hoarder bug: my held item is null when attempting to drop it!!");
                return;
            }
            GrabbableObject itemGrabbableObject = heldItem.itemGrabbableObject;
            itemGrabbableObject.parentObject = null;
            itemGrabbableObject.transform.SetParent(StartOfRound.Instance.propsContainer, worldPositionStays: true);
            itemGrabbableObject.EnablePhysics(enable: true);
            itemGrabbableObject.fallTime = 0f;
            itemGrabbableObject.startFallingPosition = itemGrabbableObject.transform.parent.InverseTransformPoint(itemGrabbableObject.transform.position);
            itemGrabbableObject.targetFloorPosition = itemGrabbableObject.transform.parent.InverseTransformPoint(targetFloorPosition);
            itemGrabbableObject.floorYRot = -1;
            itemGrabbableObject.DiscardItemFromEnemy();
            heldItem = null;
            if (!droppingInNest && ShrimpItemManager.Instance.droppedObjects.Count != 0)
            {
                ShrimpItemManager.Instance.droppedObjects.Add(itemGrabbableObject.gameObject);
            }
        }

        private void EatItem(NetworkObject item)
        {
            SyncHungerValueServerRpc(hungerValue - 20f);
            targetItem = null;
            creatureAnimator.SetTrigger("eat");
            creatureSFX.PlayOneShot(Plugin.dogEatItem);
            GrabbableObject component = item.gameObject.GetComponent<GrabbableObject>();
            component.NetworkObject.Despawn(true);
        }

        public override void OnCollideWithPlayer(Collider other)
        {
            base.OnCollideWithPlayer(other);
            if (currentBehaviourStateIndex == 2)
            {
                if (this.isEnemyDead)
                {
                    return;
                }
                PlayerControllerB playerControllerB = base.MeetsStandardPlayerCollisionConditions(other, this.inKillAnimation || startingKillAnimationLocalClient, false);
                if (playerControllerB != null)
                {
                    KillPlayerAnimationServerRpc((int)playerControllerB.playerClientId);
                    startingKillAnimationLocalClient = true;
                }
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void KillPlayerAnimationServerRpc(int playerObjectId)
        {
            if (!inKillAnimation)
            {
                inKillAnimation = true;
                KillPlayerAnimationClientRpc(playerObjectId);
                return;
            }
            CancelKillAnimationClientRpc(playerObjectId);
        }

        [ClientRpc]
        public void KillPlayerAnimationClientRpc(int playerObjectId)
        {
            StartCoroutine(KillPlayerAnimation(playerObjectId));
        }

        [ClientRpc]
        public void CancelKillAnimationClientRpc(int playerObjectId)
        {
            if ((int)GameNetworkManager.Instance.localPlayerController.playerClientId == playerObjectId)
            {
                startingKillAnimationLocalClient = false;
            }
        }

        public IEnumerator KillPlayerAnimation(int playerId)
        {
            creatureSFX.PlayOneShot(Plugin.ripPlayerApart);
            agent.speed = 0;
            agent.angularSpeed = 0;
            PlayerControllerB killPlayer = StartOfRound.Instance.allPlayerScripts[playerId];
            killPlayer.KillPlayer(Vector3.zero, true, CauseOfDeath.Mauling);
            creatureAnimator.SetTrigger("RipObject");

            float startTime = Time.realtimeSinceStartup;
            yield return new WaitUntil(() => killPlayer.deadBody != null || Time.realtimeSinceStartup - startTime > 2f);
            DeadBodyInfo body = killPlayer.deadBody;
            if (body != null && body.attachedTo == null)
            {
                body.attachedLimb = body.bodyParts[5];
                body.attachedTo = grabTarget;
                body.matchPositionExactly = true;
            }
            yield return new WaitForSeconds(0.03f);
            SyncHungerValueServerRpc(-20);
            yield return new WaitForSeconds(4f);
            creatureAnimator.SetTrigger("eat");
            creatureSFX.PlayOneShot(Plugin.dogEatPlayer);
            killPlayer.deadBody.grabBodyObject.NetworkObject.Despawn();
            yield return new WaitForSeconds(1.7f);
            agent.speed = 6;
            agent.angularSpeed = 10;
            inKillAnimation = false;
        }

        [ServerRpc(RequireOwnership = false)]
        public void HitPlayerServerRpc()
        {
            HitPlayerClientRpc();
        }

        [ClientRpc]
        public void HitPlayerClientRpc()
        {
            if (!isEnemyDead)
            {
                creatureAnimator.SetTrigger("HitPlayer");
                creatureSFX.PlayOneShot(hitPlayerSFX);
                WalkieTalkie.TransmitOneShotAudio(creatureSFX, hitPlayerSFX);
            }
        }
        public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
        {
            if (hungerValue < 40)
            {
                StartCoroutine(stunnedTimer());
                lastHitPlayer = playerWhoHit;
            }
        }

        private IEnumerator stunnedTimer()
        {
            agent.speed = 0f;
            creatureAnimator.SetTrigger("Recoil");
            creatureVoice.PlayOneShot(Plugin.cry1, 1f);
            yield return new WaitForSeconds(0.5f);
            scaredBackingAway = 2.2f;
            yield return new WaitForSeconds(0.3f);
            yield break;
        }

        public override void KillEnemy(bool destroy = false)
        {
            base.KillEnemy();
            agent.speed = 0f;
            creatureVoice.Stop();
            creatureSFX.Stop();
        }

        public GrabbableObject CheckLineOfSightForItem(float width = 45f, int range = 60, float proximityAwareness = 3f)
        {
            List<GrabbableObject> grabbableObjects = ShrimpItemManager.Instance.droppedItems;
            for (int i = 0; i < grabbableObjects.Count; i++)
            {
                if (!grabbableObjects[i].grabbableToEnemies || !grabbableObjects[i].isHeld || ShrimpItemManager.Instance.droppedItems.Count > 0)
                {
                    continue;
                }
                Vector3 position = grabbableObjects[i].transform.position;
                if (!Physics.Linecast(eye.position, position, StartOfRound.Instance.collidersAndRoomMaskAndDefault))
                {
                    Vector3 to = position - eye.position;
                    if (Vector3.Angle(eye.forward, to) < width || Vector3.Distance(base.transform.position, position) < proximityAwareness)
                    {
                        return grabbableObjects[i];
                    }
                }
            }
            return null;
        }

        private void BackAway()
        {
            Ray backAwayRay;
            RaycastHit hitInfo;
            RaycastHit hitInfoB;

            foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
            {
                float num = Vector3.Distance(base.transform.position, player.transform.position);
                if (Vector3.Distance(base.transform.position, player.transform.position) < float.PositiveInfinity && num < 30f)
                {
                    if (!player.isPlayerDead)
                    {
                        targetPlayer = player;
                    }
                }
            }
            Vector3 position;
            agent.destination = targetPlayer.transform.position;
            position = targetPlayer.transform.position;
            position.y = base.transform.position.y;

            Vector3 vector = position - base.transform.position;
            backAwayRay = new Ray(base.transform.position, vector * -1f);
            if (Physics.Raycast(backAwayRay, out hitInfo, 60f, StartOfRound.Instance.collidersAndRoomMaskAndDefault))
            {
                if (hitInfo.distance < 4f)
                {
                    if (Physics.Linecast(base.transform.position, hitInfo.point + Vector3.Cross(vector, Vector3.up) * 25.5f, out hitInfoB, StartOfRound.Instance.collidersAndRoomMaskAndDefault))
                    {
                        float distance = hitInfoB.distance;
                        if (Physics.Linecast(base.transform.position, hitInfo.point + Vector3.Cross(vector, Vector3.up) * -25.5f, out hitInfoB, StartOfRound.Instance.collidersAndRoomMaskAndDefault))
                        {
                            float distance2 = hitInfoB.distance;
                            if (Mathf.Abs(distance - distance2) < 5f)
                            {
                                agent.destination = hitInfo.point + Vector3.Cross(vector, Vector3.up) * -4.5f;
                            }
                            else
                            {
                                if (distance < distance2)
                                {
                                    agent.destination = hitInfo.point + Vector3.Cross(vector, Vector3.up) * -4.5f;
                                }
                                else
                                {
                                    agent.destination = hitInfo.point + Vector3.Cross(vector, Vector3.up) * 4.5f;
                                }
                            }
                        }
                    }
                }
                else
                {
                    agent.destination = hitInfo.point;
                }
            }
            else
            {
                agent.destination = backAwayRay.GetPoint(2.3f);
            }
            agent.stoppingDistance = 0.2f;
            Quaternion quaternion = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(vector), 3f * Time.deltaTime);
            transform.eulerAngles = new Vector3(0f, quaternion.eulerAngles.y, 0f);
            agent.speed = 8f;
            agent.acceleration = 50000;
            creatureAnimator.SetFloat("walkSpeed", -2.2f);
        }
    }
}
