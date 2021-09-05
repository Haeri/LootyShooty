using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class KillFeedUIEntry : MonoBehaviour
{
    public Text killer;
    public Text victim;

    public void CreateEntry(string killer, string victim)
    {
        this.killer.text = killer;
        this.victim.text = victim;
    }
}
