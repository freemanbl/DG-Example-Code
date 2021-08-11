using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using TMPro;

public class base_Arena_Behaviour : MonoBehaviour
{
    public List<arenaDoorWay> entrances;
    public bool inProgress;
    public bool completed;
    public Vector2Int minMaxSpawnsPerWave;
    public List<GameObject> enemyTypes;
    public List<GameObject> spawnLocations;
    public List<GameObject> enemyWave;
    private dungeonGen_V2 dungeonInfo;

    private int curWave;
    private int howManyWaves;
    private bool spawning;
    // Start is called before the first frame update
    void Start()
    {
        curWave = 1;
        dungeonInfo = dungeonGen_V2.instance;
        //enemyTypes = dungeonInfo.currentDungeonType.enemySpawns;
    }

    // Update is called once per frame
    void Update()
    {
        if(completed == false)
        {
            if (inProgress == true)
            {
                for (int i = 0; i < enemyWave.Count; i++)
                {
                    if (enemyWave[i] == null)
                    {
                        enemyWave.RemoveAt(i);
                    }
                }
                if (enemyWave.Count == 0 && spawning == false)
                {
                    if (curWave <= howManyWaves && spawning == false)
                    {
                        Debug.Log("Wave completed " + curWave);
                        StartCoroutine(spawnEnemyWave(3));
                    }
                    else if (curWave > howManyWaves)
                    {
                        Debug.Log("Arena completed");
                        endArenaEncounter();
                    }

                }
            }
        }
        
    }
    public IEnumerator spawnEnemyWave(int waitTime)
    {
        Debug.Log("spawning wave...");
        if(curWave == 1)
        {
            StartCoroutine(showHeaderUI(2f, "ARENA STARTED"));
        } else
        {
            StartCoroutine(showHeaderUI(2f, "WAVE " + curWave));
        }
        
        spawning = true;
        curWave++;
        yield return new WaitForSeconds(waitTime);
        List<GameObject> cSpawns = new List<GameObject>(); //chosen spawn points
        int enemyCount = Random.Range(minMaxSpawnsPerWave.x, minMaxSpawnsPerWave.y);
        //choose spawn locations
        for (int i = 0; i < enemyCount; i++) 
        {
            GameObject n_Spawn = spawnLocations[Random.Range(0, spawnLocations.Count)];
            while (cSpawns.Contains(n_Spawn)) //loop until not a duplicate
            {
                n_Spawn = spawnLocations[Random.Range(0, spawnLocations.Count)];
            }
            cSpawns.Add(n_Spawn);
        }
        for (int i = 0; i < cSpawns.Count; i++)
        {
            cSpawns[i].GetComponentInChildren<ParticleSystem>().Play(); //play FX
            yield return new WaitForSeconds(0.5f);
            GameObject cEnemyType = enemyTypes[Random.Range(0, enemyTypes.Count)];
            GameObject newEnemy = Instantiate(cEnemyType, new Vector3(cSpawns[i].transform.position.x, cSpawns[i].transform.position.y, cSpawns[i].transform.position.z), Quaternion.identity);
            enemySoundPlayer sndForEnemy = newEnemy.GetComponent<enemySoundPlayer>();
            sndForEnemy.source.volume = 1f;
            sndForEnemy.playSoundOneShot(sndForEnemy.spawnSound, 2f);
            sndForEnemy.source.volume = 0.5f;
            enemyWave.Add(newEnemy);
        }
        spawning = false;
    }

    public void endArenaEncounter()
    {
        completed = true;
        inProgress = false;
        foreach (arenaDoorWay dw in entrances)
        {
            dw.doorBlocker.GetComponent<Animator>().SetTrigger("openDoor");
        }
        StartCoroutine(showHeaderUI(2f, "ARENA COMPLETED"));
        doorSlamScreenShakeEffect();
    }
    public void onPlayerEnter() //triggered in arenadoor script by ontriggerenter
    {
        if(completed == false)
        {
            Debug.Log("player entered arena");
            howManyWaves = Random.Range(2, 5);
            Debug.Log("arena has " + howManyWaves + " waves");
            inProgress = true;
            foreach (arenaDoorWay dw in entrances)
            {
                dw.doorBlocker.SetActive(true);
            }
            doorSlamScreenShakeEffect();
            StartCoroutine(spawnEnemyWave(2));
        }
        
    }
    public void doorSlamScreenShakeEffect()
    {
        screenShakeCtrl.instance.StartCoroutine(screenShakeCtrl.instance.screenShake(1.2f, 0.1f, 0.1f));
    }
    public IEnumerator showHeaderUI(float time, string text)
    {
        dungeonInfo.headerUI.SetActive(true);
        dungeonInfo.headerUI.GetComponentInChildren<TextMeshProUGUI>().text = text;
        yield return new WaitForSeconds(time);
        dungeonInfo.headerUI.SetActive(false);
    }
}
[System.Serializable]
public class arenaDoorWay
{
    public bool open;
    public GameObject openObject;
    public GameObject doorBlocker;
    public GameObject closedObject;
}
