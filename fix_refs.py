import os
import re

def fix_refs(filepath):
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()

    # Replacements:
    # typeof(VelocityOverride. -> typeof(VelocityOverrides.
    # typeof(Kinematic. -> typeof(Kinematics.
    # typeof(PID. -> typeof(PIDs.
    # typeof(Force. -> typeof(Forces.
    
    # using BovineLabs.Timeline.Physics.Force; -> using BovineLabs.Timeline.Physics.Forces;
    # etc. for all renames
    
    renames = {
        'Drag': 'Drags',
        'Force': 'Forces',
        'Gravity': 'Gravities',
        'Kinematic': 'Kinematics',
        'PID': 'PIDs',
        'Ricochet': 'Ricochets',
        'Smear': 'Smears',
        'VelocityClamp': 'VelocityClamps',
        'VelocityOverride': 'VelocityOverrides'
    }
    
    new_content = content
    for old, new in renames.items():
        # Fix typeof
        new_content = re.sub(rf'typeof\({old}\.', f'typeof({new}.', new_content)
        # Fix usings
        new_content = re.sub(rf'using BovineLabs\.Timeline\.Physics\.{old};', f'using BovineLabs.Timeline.Physics.{new};', new_content)

    if new_content != content:
        with open(filepath, 'w', encoding='utf-8') as f:
            f.write(new_content)
        print(f"Updated references in {filepath}")

if __name__ == '__main__':
    base_dir = '/home/i/GitHub/BovineLabs/Packages/BovineLabs.Timeline.Physics/BovineLabs.Timeline.Physics'
    for root, dirs, files in os.walk(base_dir):
        for f in files:
            if f.endswith('.cs'):
                fix_refs(os.path.join(root, f))
