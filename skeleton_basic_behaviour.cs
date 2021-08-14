using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class skeleton_basic_behaviour : basicEnemy
{
    private Animator anim;
    public float attackSpeedCounter, attackSpeedThreshold;
    float distance;
    public bool inLOS; //can see player
    public bool attacking = false;
    public LayerMask aimRayLayerMask;
    bool walking;
    // Start is called before the first frame update
    public override void Start()
    {
        base.Start();
        anim = GetComponentInChildren<Animator>();
    }
    private void FixedUpdate()
    {
        attackSpeedThreshold = timeForAtk * 50;
        attackSpeedCounter = Mathf.Clamp(attackSpeedCounter, 0, attackSpeedThreshold + 1);
        attackSpeedCounter++;
    }
    public override void Update()
    {
        base.Update();
        distance = Vector3.Distance(transform.position, player.transform.position);
        if (Health <= 0 && state != "dead")
        {
            //clear all animations queued
            state = "dead";
            anim.ResetTrigger("attack");
            anim.ResetTrigger("hit");
            anim.ResetTrigger("idle");
            anim.ResetTrigger("StartWalk");
            anim.SetTrigger("die");
        }
        
        switch (state)
        {
            case "idle":
                if (distance < viewRange)
                {
                    aggroRanged();
                    state = "pursuit";
                }
                break;
            case "pursuit":
                if (distance < attackRange) //within attack range
                {
                    faceTarget();//Look toward player
                    //----LOS Detection
                    RaycastHit aHit;
                    string[] ignoreLayers = { "Enemy, enemyAttack, ragdoll" };
                    Ray aimRay = new Ray(new Vector3(transform.position.x, transform.position.y + 1, transform.position.z), directionToPlayer);
                    if (Physics.Raycast(aimRay, out aHit, viewRange, aimRayLayerMask))
                    {
                        if (aHit.collider.tag == "Player")
                        {
                            inLOS = true;
                        } else { inLOS = false;}
                    }
                }
                //----Determine behaviour
                aggroRanged();
                navAgent.isStopped = attacking;
                break;
            case "stunned":
                navAgent.isStopped = true;
                //navAgent.updateRotation = false;
                break;
            case "dead": //die
                if(dead == false)
                {
                    die();
                    dead = true;
                }
                state = "waitingToDestroy";
                break;
            case "waitingToDestroy":
                //null, to prevent die() from being called more than once
                break;
        }
    }
    public void attack()
    {
        Vector3 dir = directionToPlayer;
        attackSpeedCounter = 0;
        GameObject shot = Instantiate(Resources.Load("prefabs/enemyProjectile"), new Vector3(transform.position.x, transform.position.y + 1, transform.position.z), Quaternion.identity) as GameObject;
        basicEnemyProjectile info = shot.GetComponentInChildren<basicEnemyProjectile>();
        info.damage = attackDamage;
        info.shootDir = directionToPlayer;
        attacking = false;
    }
    public void aggroRanged()
    {
        if (attacking == true && inLOS == false) //if was attacking but player moved out of LOS, cancel the attack
        {
            cancelAttack();
        }
        if(attacking == false)
        {
            //----Ticking boxes
            bool goodDistance;
            if (distance < attackRange) //are we in attack range?
            {
                goodDistance = true;
            }
            else
            {
                goodDistance = false;
            }
            if(distance < viewRange && distance > attackRange) //can see the player, but not close enough to attack
            {
                navAgent.isStopped = false;
                navAgent.SetDestination(player.transform.position);
                if(walking == false)
                {
                    anim.SetTrigger("StartWalk");
                }
                walking = true;
                attacking = false;
            }
            if (inLOS == true && goodDistance == true) //can see and in range, stop and shoot
            {
                navAgent.isStopped = true;
                navAgent.ResetPath();
                walking = false;
                if (attacking == false && playerStats.alive == true && attackSpeedCounter >= attackSpeedThreshold)
                {
                    anim.SetTrigger("attack");
                    attacking = true;
                }
            } else if (inLOS == false && distance < viewRange) //if not in LOS, go closer to get LOS on player
            {
                navAgent.isStopped = false;
                navAgent.SetDestination(player.transform.position);
                if(walking == false)
                {
                    anim.SetTrigger("StartWalk");
                }
                attacking = false;
                walking = true;
            }
            if (distance > viewRange * 2) //if out of view range, lose aggro
            {
                state = "idle";
                walking = false;
                anim.SetTrigger("idle");
            }
        }
        
    }
    public void cancelAttack()
    {
        Debug.Log("Cancelled attack");
        attacking = false;
        navAgent.SetDestination(player.transform.position);
        anim.SetTrigger("StartWalk");
    }
    public override void onHit(int damage)
    {
        Health -= damage;
        if (Health > 0)
        {
            //anim.SetTrigger("hit");
        }
        //sndPlayer.playSound(sndPlayer.hitSound); //implement later
    }
    public override void die()
    {
        navAgent.ResetPath();
        navAgent.isStopped = true;
        playerStats.instance.exp += Mathf.RoundToInt(expDrop * playerStats.instance.expBonus);
        //restore some player health regen cap
        playerStats.instance.healthRecCap += 5;
        //remove self from enemies alive list
        //loot roll
        if (gunModDropManager.instance.rollDropChance() == true)
        {
            gunModDropManager.instance.dropGunModAtPoint(null, new Vector3(transform.position.x, transform.position.y + 2, transform.position.z));
        }
        StartCoroutine(delayedDestroyFX());
    }
    private IEnumerator delayedDestroyFX()
    {
        yield return new WaitForSeconds(3);
        GameObject bloodFX = Instantiate(Resources.Load("prefabs/particles/bloodyExplosion"), transform.position, Quaternion.identity) as GameObject;
        Destroy(gameObject);
    }
}
