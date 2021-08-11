using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
[System.Serializable]
public class playerStats : MonoBehaviour
{
    public static playerStats instance;
    public playerLevelReq lvlReq;
    [Header("Defensive")]
    public static bool alive;
    public float healthRecoverySpeed, healthRecWaitTime;
    public int healthRecCap;
    public int maxHealth, health; //base stats, increase with level
    [HideInInspector]public int healthBonus; //added from items
    [Header("Offensive")]
    public int attackDmg; //BASE STATS, increases with level
    public float attackSpeed;
    public float critMulti; //how much to multiply critical hits by
    [Header("From items (*)")]
    public float attackDmgBonus = 1;
    public float expBonus; //added from items
    [Header("From items (+)")]
    public float attackSpeedBonus; //ADDED FROM ITEMS, damage should be PERCENTAGE based
    public float critChanceBonus, critMultiBonus; //added from items
    public float recoilModifier = 0.4f;
    public float moveSpeedModifier;
    
    [Header("Progression")]
    public int exp;
    public int level;
    
    public Slider healthBar, recoveryCapBar;
    public TextMeshProUGUI healthText, lvlText;
    public Slider xpSlider;
    public GameObject lvlUpUI;
    [Header("Assignments")]
    public GameObject playerInfoParentUI;
    public Animator cameraAnim;
    public Animator deathScreenAnim;
    public randomSoundPlayer hitHurtSoundPlayer;
    public randomSoundPlayer deathSoundPlayer;
    public Slider atkSpeedVisual;
    public Image atkSpdVisImg;
    private int startAttackDmg, startMaxHealth;
    private float startCritChance, startCritMulti, startAttackSpeed; //start stats for reference

    private float healthRecTimer, healthRecActiveTickTime;
    private void Awake()
    {
        instance = this;
        lvlReq = GetComponent<playerLevelReq>();
        alive = true;
        playerInfoParentUI.SetActive(true);
        startAttackDmg = attackDmg;
        startAttackSpeed = attackSpeed;
        startMaxHealth = maxHealth;
        startCritMulti = critMulti;
        healthRecCap = maxHealth;
    }
    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.L))
        {
            exp += 50;
        }
        if (Input.GetKeyDown(KeyCode.O))
        {
            takeDamage(1);
        }
        //CLAMPS
        attackSpeed = Mathf.Clamp(attackSpeed, 1, 100);
        recoilModifier = Mathf.Clamp(recoilModifier, 0.05f, 1f);
        health = Mathf.Clamp(health, 0, maxHealth);
        healthRecCap = Mathf.Clamp(healthRecCap, 0, maxHealth + 25);
        healthRecTimer = Mathf.Clamp(healthRecTimer, 0, 5);
        attackDmgBonus = Mathf.Clamp(attackDmgBonus, 0.1f, 10f);
        attackSpeed = Mathf.Clamp(attackSpeed, 1, 20);
        //----Level up and exp overflow
        if (exp >= lvlReq.levelList[level - 1]) //the amount of exp needed for the next level
        {
            int expOverflow = exp - lvlReq.levelList[level - 1]; //if you gain more experience than needed to level up, this will give you the overflow into the next level
            exp = expOverflow;
            levelUp();
        }
        //Check if dead
        if (health == 0)
        {
            if(alive == true)
            {
                Debug.Log("in update health is " + health);
                StartCoroutine(playerDeath());
            }
        }
        //----UI Updates
        healthBar.maxValue = maxHealth;
        healthBar.value = Mathf.Lerp(healthBar.value, health, 0.1f);
        recoveryCapBar.maxValue = maxHealth;
        recoveryCapBar.value = Mathf.Lerp(recoveryCapBar.value, healthRecCap, 0.1f); ;
        healthText.text = health + "/" + maxHealth;
        xpSlider.maxValue = lvlReq.levelList[level];
        xpSlider.value = Mathf.Lerp(xpSlider.value, exp, 0.05f);
        lvlText.text = "lv " + level.ToString();
        //----Attack Speed Visualiser
        if (atkSpeedVisual.value >= atkSpeedVisual.maxValue)
        {
            var nColor = atkSpdVisImg.color;
            nColor.a = Mathf.Lerp(nColor.a, 0.15f, 0.01f);
            atkSpdVisImg.color = nColor;
        }
        else
        {
            var nColor = atkSpdVisImg.color;
            nColor.a = 1f;
            atkSpdVisImg.color = nColor;
        }
        //----health regen
        if (healthRecTimer < 5)
        {
            healthRecTimer += Time.deltaTime;
        } else
        {
            healthRecActiveTickTime += Time.deltaTime;
        }
        if(healthRecActiveTickTime >= healthRecoverySpeed && health < maxHealth && health < healthRecCap)
        {
            health += 1;
            healthRecActiveTickTime = 0;
        }
    }
    public void updateBonusStats() //called when item is picked up
    {
        float healthPercentage = health / maxHealth; //so we can apply the same percentage
        maxHealth = Mathf.RoundToInt((100 + 5 * (level - 1)) * item_exchangeCard.HealthMod) + healthBonus;
        Debug.Log("hmod = " + item_exchangeCard.HealthMod);
        //health = Mathf.RoundToInt(maxHealth * healthPercentage); //keep same percentage of health

        //INCREASE BASE DAMAGE -- NOT IN USE
        int baseDmgForLvl = Mathf.RoundToInt(startAttackDmg + (2 * (level - 1)));
        Debug.Log("damage is " + startAttackDmg + (2 * (level - 1)) + " * " + attackDmgBonus);
        attackDmg = Mathf.RoundToInt((baseDmgForLvl * attackDmgBonus) * item_exchangeCard.DmgMod);
        attackSpeed = startAttackSpeed + attackSpeedBonus; //might make this increase with level later, not sure right now
        critMulti = startCritMulti + critMultiBonus;
    }
    public void takeDamage(int dmgTaken)
    {
        if(alive == true)
        {
            health -= calcDmgTaken(dmgTaken); //calc with item effects
            this.BroadcastMessage("onHitPlayer");
            screenShakeCtrl.instance.StartCoroutine(screenShakeCtrl.instance.screenShake(0.1f, 0.2f, 0.2f));
            hitHurtSoundPlayer.playSound();
            //reset regen timer
            healthRecTimer = 0;
            healthRecActiveTickTime = 0;
            if (health < healthRecCap - 25)
            {
                healthRecCap = health + 25;
            }
        }
        
    }
    private int calcDmgTaken(int d)
    {
        float cushionModifier = item_cushion.instance.active == true ? item_cushion.instance.Trigger() : 1;//will return 1 if doesn't land roll
        float modifier = cushionModifier;
        //Debug.Log(modifier);
        return Mathf.RoundToInt(d * modifier);
    }
    public IEnumerator playerDeath()
    {
        Debug.Log("at playerDeath health is " + health);
        alive = false;
        playerInfoParentUI.SetActive(false);
        deathSoundPlayer.playSound();
        cameraAnim.enabled = true;
        cameraAnim.SetTrigger("playerDeath");
        yield return new WaitForSeconds(2);
        deathScreenAnim.SetTrigger("playerDeath");

    }
    private void levelUp()
    {
        level++;
        updateBonusStats();
        lvlUpUI.SetActive(true);
        levelUpItemSequence.levelsToGo++;
    }
}
