using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Shapes2D;

public class HitIndicator : MonoBehaviour
{
    public bool charging;
    public float chargeTime;
    public float chargeStartTime;
    public float chargeEndTime;
    public bool recovering;
    public float recoverTime;
    public float recoverStartTime;
    public float recoverEndTime;
    public Shape charge;
    public Shape recover;
    public Shape crit;
    private float zero = 1f;

    public void Start()
    {
        charge.gameObject.SetActive(false);
        charge.settings.endAngle = zero;
        recover.gameObject.SetActive(false);
        crit.gameObject.SetActive(true);
    }

    public void SetCritRange(float min, float max)
    {
        crit.settings.startAngle = min * 360f;
        crit.settings.endAngle = max * 360f;
    }

    public void Pulse(float chargeTime)
    {
        EndCharge();
        StartRecover(chargeTime);
    }
    public void StartCharge(float chargeTime)
    {
        charging = true;
        this.chargeTime = chargeTime;
        chargeEndTime = Time.time + chargeTime;
        charge.settings.endAngle = zero;
        charge.gameObject.SetActive(true);
        recover.gameObject.SetActive(false);
        chargeStartTime = Time.time;
    }

    public void EndCharge()
    {
        charging = false;
        charge.settings.endAngle = zero;
        chargeEndTime = Time.time;
        charge.gameObject.SetActive(false);
        recover.gameObject.SetActive(true);
        recoverStartTime = Time.time;
    }

    public void StartRecover(float recoverTime)
    {
        recovering = true;
        this.recoverTime = recoverTime;
        recover.settings.endAngle = 360f;
        recover.gameObject.SetActive(true);
        recoverStartTime = Time.time;
        recoverEndTime = Time.time + recoverTime;
    }

    public void EndRecover()
    {
        recovering = false;
        recover.settings.endAngle = zero;
        recoverEndTime = Time.time;
        recover.gameObject.SetActive(false);
    }

    public void Update()
    {
        if (charging)
        {
            float t = (Time.time - chargeStartTime) / (chargeEndTime - chargeStartTime);
            charge.settings.endAngle = Mathf.Lerp(0f, 360f, t);
        }
        else if (recovering)
        {
            float t = (Time.time - recoverStartTime) / (recoverEndTime - recoverStartTime);
            recover.settings.endAngle = Mathf.Lerp(360f, 0f, t);
            if (t >= 1f)
            {
                EndRecover();
            }
        }
    }
    }
