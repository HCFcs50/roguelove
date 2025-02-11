using System.Collections.Generic;
using UnityEngine;

public class DialogueSource : MonoBehaviour
{

    [Header("SCRIPT REFERENCES")]

    [SerializeField] private PlayerController player;

    [SerializeField] private Animator useAnimator;

    public DialogueList dialogueList;

    [SerializeField] DialogueManager dialogueManager;

    [Header("STATS")]
    
    [SerializeField] protected bool inRadius;

    private void Start() {
        dialogueManager = GameStateManager.dialogueManager;
    }

    public virtual void OnTriggerEnter2D(Collider2D collider) {

        // If player is within radius, set true
        if (collider.CompareTag("Player")) {

            // Show use indicator
            UseIndicator(true);

            if (player == null) {
                player = collider.gameObject.GetComponent<PlayerController>();
            }

            inRadius = true;
        } 
    }

    public virtual void OnTriggerExit2D(Collider2D collider) {

        // If player leaves radius, set false
        if (collider.CompareTag("Player")) {

            // Hide use indicator
            UseIndicator(false);

            inRadius = false;
        } 
    }

    public virtual void Update() {

        if (inRadius && Input.GetKeyDown(KeyCode.E) && !dialogueManager.playingDialogue && GameStateManager.GetState() == GAMESTATE.PLAYING) {

            AddDialogue();

            // Create new priority list only for this dialogue source (e.g. only Fallow dialogue)
            List<DialoguePiece> sourceList = new()
            {
                dialogueManager.priority.Find(x => x.id.Contains(dialogueList.id_prefix.ToString()) == true)
            };
            sourceList.Sort();

            GameStateManager.dialogueManager.StartDialogue(sourceList[0], false);

        }
    }

    // Show/hide use indicator
    public virtual void UseIndicator(bool value) {

        if (useAnimator) {
            useAnimator.SetBool("Show", value);
        }
    }

    // Check if dialogue with requirements can be added, and if not, then add a noReq dialogue
    public virtual void AddDialogue() {

        int counter = 0;

        // REQUIREMENTS DIALOGUE

        dialogueManager.CheckIfEmpty(dialogueList.requirements, dialogueList.seenRequirements);

        // For every dialogue with a requirement—
        foreach (DialoguePiece dialogue in dialogueList.requirements) {

            // For every requirement that piece of dialogue has—
            for (int i = 0; i < dialogue.requirements.Count; i++) {

                // Check the requirements
                if (dialogueManager.CheckRequirements(dialogue.requirements[i], player)) {

                    // Add it to the priority list if all requirements have been fulfilled
                    dialogueManager.priority.Add(dialogue);
                    counter++;
                    Debug.Log("Added a fulfilled requirement dialogue!");
                }
            }
        }

        // NO REQUIREMENTS DIALOGUE

        // If no requirements dialogue can be displayed—
        if (counter == 0) {

            // Add a random no requirements dialogue
            dialogueManager.AddRandomNoReqDialogue(dialogueList.noRequirements, dialogueList.seenNoRequirements);
        }
    }

}
