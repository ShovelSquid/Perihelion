# Helios — Raw Notes (original brainstorm)

> Preserved verbatim from the original voice-memo-style brainstorm. The structured
> version lives in [CustomHelios.md](CustomHelios.md). Keep this around as the source
> of the raw ideas; edit the structured doc, not this one.

---

HeliOS

Separate logic and mathematical algorithms/data generation from rendering. I believe that to be a core principle; even if its not done programmatically, it should be done psychologically. The rendering should be simple where the maths are complex. The maths are fundamentally simple, and should remain such. Yet, the rendering should reflect that; being simple at base, with complexity being derived from the necessity of the simple physics. Minecraft blockiness with realistic physics, makes the coolest thing, aeronautics mod.

entity is represented by model
- animation plays according to state; behavior. Current action/movement.
    - idle, emotion
    - watchful, emotion.
        - base action reflected by emotion?
        - idle : watchful vs idle : lazy. or tired.
- name displays for each entity, must be accessible.
- Relationship lines display for each entity.
- Vector fields full of nodes MUST be visible, and interactable.

what is an entity?
a collection of nodes in a node.
What is a node?
an arbitrary trait defined by the world.
Where are the nodes?
in n dimensional space, being able to subspace.
How does camera move?
In different controllable ways; either framing all selected, all important, all favorited, or zooming in on highlighted. Differencve between highlighted and selected? different arbitrary nameable layers for organization.
Sound files. Included.
- Does AI turn coding into an artform? it can.
- turn sound waves into different amplitudes with hand.
What is format?
- Create node, or Note. Each note has many sub lists, as many as necessary.
    - dotted list, like this!
    - can use them as subnodes or not. Each node or note can attach to an entity, and an entity can carry around a selection of notes.
        - does not like x.
        - loves y.
        - is afraid of z.
    - these are all note-examples (above). How above or below can you go?
- Entities model and track their relationships to other entities, through the knowledge of the notes that other entities possess.
    - Each entity discovers the notes within other entities, even if they are right or wrong.
    - They build up worlds within each entity.
- Over t, entities can move, and notes can be discovered, written, erased, rewritten, etc. We need to be able to model each of these changes with simple logs, which suggests a deterministic world.
    - Can local LLMs be deterministic in their output? of course.
- That's it. entities can be killed, or created. each requires material, or doesn't. rules must be established.
    - rules are created at the start of every game.
    - They are intrinsic, and can be rewritten (if you have admin privileges).

- rules determine how movement occurs.
    - changes to notes, or changes to stats.
- entities can break into other entities.
    - is this emergent behavior?
- notes are the key to life.
    - can be any length and any sound. incur the wrath of sonic qualities and linguistic ones.
- entities can create behavior trees?
    - rather blocks of behaviors, a list of behaviors to do, in an order once sequences are completed.
    - entities can program themselves, basically. With building block functions.
- functions should be treated as building blocks.
    - we have functions now.
    - for actions for entities to take. at any given time.
    - entities choose when and were to execute functions.
        - can create function trees, or DNA…?
    - 4 functions? A T B G E? that's 5 dumbass

- notes can be attached to entities, or not. or note.
    - can either be written in physical world space, attached to entities (persons or objects), or written in the ambiguous white space.
        - white space and gray space
            - white space: the blank page. rule, event, character creation, no care or consideration for placement in world.
            - gray space: world space. where events, rules, characters are created and placed within time and space.
    - creating notes in white space means they must be allocated; either to the overarching gray space (i.e., rules), or to entities within.

- organized like a blender tree file
    - we have the gray space, which is the container for the world. Within, we have entities, which have containers for other entities.
        - folders can contain entities. are they entities? no, because they have no position. Or? is entities so barebones?
        - entities relate to things with position. folders are a type of datablock, which are simply entities without transforms. transforms should be able to apply to entities; so entities can exist within whitespace without existing in gray space? they exist within gray space, yet not in any space. yet are apart of both the gray and white space.
            - gray space is the sandbox, white space is where input gets filtered into the sandbox.
    - entities have components.
        - transform, notes. functions. I mean, transforms could be the most simple note type.
            -  core notes and player notes? differentiables? they should be distinct, even if not mechanically, at least discretely. How they are labeled to the player/writer.

- camera must be controlled.
    - at different times, place cameras in different locations. take snapshots.
        - keyframes? notes? copy all notes, locations, and everything at different positions.
            - are keyframes the basic datablock for player input to recreate simulation deterministically?
    - need different scenes, and events.

- are events a type of node?
    - describing which entities are apart of it, and what functions take place, and what the note outcomes are?
        - notes: x dies, y triumphs.
        - functions: built from t(0) to t(1) to recreate that outcome.
    - can be solved the other way around.
        - what functions begin, what functions continue to play out.

- need to solve functions and events dynamically and multi-linearly.
    - creating histories. filling in gaps.

- entities must need all vector inputs for light, images, and for body control. These values should not be abstracted to notes; notes should be a psychological component to connect player intent to avatar behavior. Something easy to digest and make sense of, while providing real vector value without needing to be discretized to a core component of the simulation. Everything simulated should be a vector field, in fact, notes should affect the vector fields of various variables. in concrete and visible ways.
    - everything should be visible; from every functions impact on other entities' neural networks, to relationships between neural networks, to position and velocity changes (vector values graphed over t).
    - all of this should serve to create a blend between mathematics and the real concrete world. An intuitive understandable blend of analog values that can be adjusted finely, immersively connecting people to the math.
