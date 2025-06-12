using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class SettingsMenuViewController : LLBasePanel
{

    [SerializeField] GridLayoutGroup buttonGrid;
    [SerializeField] GameObject buttonPrefab;
    [SerializeField] GameObject previousButton;
    [SerializeField] GameObject nextButton;

    private int currentPage = 0;
    private int maxPage = 0;
    private List<LablightSettings> settings = new List<LablightSettings>();


    List<SettingsMenuButton> buttons = new List<SettingsMenuButton>();
    // Start is called before the first frame update
    protected override void Awake()
    {
        base.Awake();
    }

    // Update is called once per frame
    void Start()
    {
        foreach(LablightSettings setting in LablightSettings.GetValues(typeof(LablightSettings))) //initialize dictionary with all settings in enum
        {
            Debug.Log("adding setting: " + setting.ToString());
            settings.Add(setting);
        }
    }

    void OnEnable()
    {
        currentPage = 0;
        StartCoroutine(DelayBuildPage(0.1f));
    }

    void Build(int pageNum)
    {
        for(int i = 0; i < buttonGrid.transform.childCount; i++)
        {
            buttonGrid.transform.GetChild(i).gameObject.SetActive(false);
            Destroy(buttonGrid.transform.GetChild(i).gameObject);
        }

        int index = 0;
        foreach(LablightSettings setting in LablightSettings.GetValues(typeof(LablightSettings)))
        {
            if(index >= pageNum * 8 && index < (pageNum + 1) * 8)
            {
                var currSetting = setting;
                var button = Instantiate(buttonPrefab, buttonGrid.transform);
                SettingsMenuButton buttonScript = button.GetComponent<SettingsMenuButton>();
                buttons.Add(buttonScript);
                button.GetComponent<SettingsMenuButton>().Initialize(setting);
            }
            index++;
        }
    }
    private void OnDisable()
    {
        foreach (var button in buttons)
        {
           if (button != null)
           {
               Destroy(button.gameObject);
           }
        }
        buttons.Clear();
    }
    public void NextPage()
    {
        if (currentPage < maxPage - 1)
        {
            currentPage++;
            Build(currentPage);
        }
    }

    public void PreviousPage()
    {
        if (currentPage > 0)
        {
            currentPage--;
            Build(currentPage);
        }
    }

    private IEnumerator DelayBuildPage(float delay)
    {
        yield return new WaitForSeconds(delay);
        maxPage = (int)Math.Ceiling((double)settings.Count / 8);
        Build(currentPage);
    }

    public void CloseSettingsMenu()
    {
        SceneLoader.Instance.UnloadScene("Settings");
    }
}
