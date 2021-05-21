using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIFader : MonoBehaviour
{
    public float fadeSpeed = 1.0f;

    private Image _image;
    private float _alpha;
    private float _delta;

    private void Awake()
    {
        _image = GetComponent<Image>();    
    }


    public void ResetFade()
    {
        gameObject.SetActive(true);

        _delta = 0;
        _alpha = 1;
        //Color col = _image.color;
        //col.a = 1.0f;
        //_image.color = col;
    }

    private float linearStep(float x)
    {
        return x;
    }

    private float quadraticStep(float x)
    {
        return Mathf.Pow(x, 5);
    }

    void Update()
    {
        if (_alpha > 0.001f)
        {
            _delta += fadeSpeed * Time.deltaTime;
            _alpha = Mathf.Lerp(1.0f, 0.0f, quadraticStep(_delta));

            Color col = _image.color;
            col.a = _alpha;
            _image.color = col;
        }
        else
        {
            gameObject.SetActive(false);
        }
    }   
}
