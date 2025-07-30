using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ChangeNumberFromSlider : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI number;

    public void UpdateNumber(float n)
    {
        number.text = $"{n/10f}/sec";
    }
   
}
