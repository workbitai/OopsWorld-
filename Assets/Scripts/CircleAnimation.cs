using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;

public class CircleAnimation : MonoBehaviour
{
    private void Start()
    {
        this.transform.DOScale(1.7f, .4f).SetEase(Ease.OutSine);
        DOVirtual.DelayedCall(.2f, () => 
        {
            this.transform.GetComponent<Image>().DOFade(0f, .2f).OnComplete(() => Destroy(this.gameObject));
        });
    }
}
