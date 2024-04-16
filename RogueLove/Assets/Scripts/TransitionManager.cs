using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TransitionManager : MonoBehaviour
{

    [SerializeField]
    private Animator animator;

    [SerializeField]
    private GameObject loadingScreen;

    [SerializeField]
    private Slider slider;

    [SerializeField]
    private float loadTime = 1f;

    private static int start = -1;
    public static void StartLeaf(int index) {
        start = index;
    }

    private static bool end;
    public static void EndLeaf(bool condition) {
        end = condition;
    }

    private static int loading;
    public static void SetLoading(int num) {
        loading = num;
    }

    private static bool loadingBarEnable;
    public static void SetLoadingBar(bool condition) {
        loadingBarEnable = condition;
    }

    // Start is called before the first frame update
    void Start()
    {
        //DontDestroyOnLoad(this);
        loadingBarEnable = false;
    }

    // Update is called once per frame
    void Update()
    {
        if (start != -1) {
            StartCoroutine(LoadLevel(start));
            start = -1;
        }
        if (end == true) {
            end = false;
            EndLoad();
        }
        if (loadingBarEnable == true) {
            loadingScreen.SetActive(true);
        } else {
            loadingScreen.SetActive(false);
        }
    }

    public void EndLoad() {
        SetLoadingBar(false);
        animator.SetTrigger("LeafEnd");
    }

    IEnumerator LoadLevel(int index) {
        animator.SetTrigger("LeafStart");

        yield return new WaitForSeconds(loadTime);

        SetLoadingBar(true);

        AsyncOperation operation = SceneManager.LoadSceneAsync(index);

        while (!operation.isDone) {
            float progress = Mathf.Clamp01(operation.progress / 0.9f);
            Debug.Log(progress);

            //slider.value = progress;

            yield return null;
        }

        if (index == 0) {
            animator.SetTrigger("LeafEnd");
            SetLoadingBar(false);
            yield return null;
        }
    }
}
