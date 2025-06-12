using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestUi : MonoBehaviour
{
    private void Awake()
    {

        ProtocolState.Instance.SetProtocolDefinition(CreateTestProtocol());
        ProtocolState.Instance.ProtocolTitle.Value = ProtocolState.Instance.ActiveProtocol.Value.title;
        Debug.Log("Test protocol set");
    }

    private List<CheckItemDefinition> CreateCheckItems(int num)
    {
        //create check items
        List<CheckItemDefinition> checkItems = new List<CheckItemDefinition>();
        for (int i = 0; i < num; i++)
        {
            CheckItemDefinition newCheckItem = new CheckItemDefinition();
            newCheckItem.Text = "checkItem " + i;
            checkItems.Add(newCheckItem);
        }
        return checkItems;
    }

    private List<StepDefinition> CreateSteps(int num)
    {
        List<StepDefinition> steps = new List<StepDefinition>();
        for(int i = 0; i < num; i++)
        {
            StepDefinition newStep = new StepDefinition();
            
            // Create a ContentItem for text instead of TextItem
            ContentItem textContent = new ContentItem
            {
                contentType = "Text",
                properties = new Dictionary<string, object>
                {
                    { "text", $"text item on step {i}" }
                }
            };
            
            newStep.contentItems.Add(textContent);
            newStep.checklist = CreateCheckItems(10);
            steps.Add(newStep);
        }
        return steps;
    }

    private ProtocolDefinition CreateTestProtocol()
    {
        ProtocolDefinition testProtocol = new ProtocolDefinition();
        testProtocol.title = "Test Protocol";
        testProtocol.steps = CreateSteps(10);
        return testProtocol;
    }
}
