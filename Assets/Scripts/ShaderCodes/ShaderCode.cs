using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class ShaderCode : MonoBehaviour
{
    
    Image image;
    Material m;

    public Editions edition = Editions.REGULAR;

    //public string[] edition = { "REGULAR", "POLYCHROME", "NEGATIVE" };

    // Start is called before the first frame update
    void Start()
    {
        image = GetComponent<Image>();
        m = new Material(image.material);
        image.material = m;

        for (int i = 0; i < image.material.enabledKeywords.Length; i++)
        {
            image.material.DisableKeyword(image.material.enabledKeywords[i]);
        }
        image.material.EnableKeyword("_EDITION_" + edition.ToString().ToUpper());
    }

    // Update is called once per frame
    void Update()
    {

        // Get the current rotation as a quaternion
        Quaternion currentRotation = transform.parent.localRotation;

        // Convert the quaternion to Euler angles
        Vector3 eulerAngles = currentRotation.eulerAngles;

        // Get the X-axis angle
        
        float xAngle = eulerAngles.x;
        float yAngle = eulerAngles.y;

        // Ensure the X-axis angle stays within the range of -90 to 90 degrees
        xAngle = ClampAngle(xAngle, -90f, 90f);
        yAngle = ClampAngle(yAngle, -90f, 90f);

        m.SetVector("_Rotation", new Vector2(ExtensionMethods.Remap(xAngle, -20, 20, -.5f, .5f), ExtensionMethods.Remap(yAngle, -20, 20, -.5f, .5f)));
        

        //float zAngle = ClampAngle(eulerAngles.z, -90f, 90f);

        //m.SetVector("_Rotation", new Vector2(xAngle, yAngle));
        //m.SetVector("_Rotation", new Vector2(ExtensionMethods.Remap(zAngle, -20, 20, -.5f, .5f), 0f));
    }

    // Method to clamp an angle between a minimum and maximum value
    float ClampAngle(float angle, float min, float max)
    {
        if (angle < -180f)
            angle += 360f;
        if (angle > 180f)
            angle -= 360f;
        return Mathf.Clamp(angle, min, max);
    }
}
