using UnityEngine;
using TMPro;
using UnityEngine.UI;
public class NpcNickAndFlag : MonoBehaviour
{

    public string npc_Name;
    [SerializeField] private NamesDataBase namesDataBase;
    [SerializeField] private TMP_Text NameText;
    [SerializeField] private Image FlagImage;
 
    void Start()
    {
        npc_Name = SetRandomName();
        NameText.text = npc_Name;
        FlagImage.sprite = SetRandomFlag();
    }

    public string SetRandomName()
    {
      if(namesDataBase != null)
        {
            return namesDataBase.names[Random.Range(0, namesDataBase.names.Length)]; 
        } 

        return "Name"; 
    }

    public Sprite SetRandomFlag()
    {
        if(namesDataBase != null)
        {
            return namesDataBase.flags[Random.Range(0, namesDataBase.flags.Length)];
        }
        return null;
    }

}

  

