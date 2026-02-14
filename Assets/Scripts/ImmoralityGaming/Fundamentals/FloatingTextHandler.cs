using ImmoralityGaming.Fundamentals;
using UnityEngine;

public class FloatingTextHandler : SingletonBehaviour<FloatingTextHandler>
{
    [SerializeField]
    private ObjectPooler objectPooler = null;

    public void CreateFloatingText(Vector3 targetLocation, string text, Color color, TextFadeMode fadeMode = TextFadeMode.FadeNormal)
    {
        var obj = objectPooler.GetPooledObject();
        obj.transform.position = new Vector3 { x = targetLocation.x, y = targetLocation.y, z = -1 }; 
        obj.GetComponent<TextMesh>().text = text;
        obj.GetComponent<TextMesh>().color = color;
        var floatingText = obj.GetComponent<FloatingText>();
        floatingText.fadeMode = fadeMode;
        obj.SetActive(true);
    }

    public void CreateFloatingText(Vector3 targetLocation, string text, Color color, float fadeSpeed, TextFadeMode fadeMode = TextFadeMode.FadeNormal)
    {
        var obj = objectPooler.GetPooledObject();
        obj.transform.position = new Vector3 { x = targetLocation.x, y = targetLocation.y, z = -1 };
        obj.GetComponent<TextMesh>().text = text;
        obj.GetComponent<TextMesh>().color = color;
        var floatingText = obj.GetComponent<FloatingText>();
        floatingText.fadeMode = fadeMode;
        floatingText.fadeSpeed = fadeSpeed;
        obj.SetActive(true);
    }

    public void CreateFloatingText(Vector3 targetLocation, string text, Color color, float fadeSpeed, float fadeRange, TextFadeMode fadeMode)
    {
        var obj = objectPooler.GetPooledObject();
        obj.transform.position = new Vector3 { x = targetLocation.x, y = targetLocation.y, z = -1 };
        obj.GetComponent<TextMesh>().text = text;
        obj.GetComponent<TextMesh>().color = color;
        var floatingText = obj.GetComponent<FloatingText>();
        floatingText.fadeMode = fadeMode;
        floatingText.fadeSpeed = fadeSpeed;
        floatingText.fadeRange = fadeRange;
        obj.SetActive(true);
    }
}
