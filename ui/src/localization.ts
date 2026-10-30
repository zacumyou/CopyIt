import {useLocalization} from "cs2/l10n";
import {createText} from "./text";
export function useText(){const {translate}=useLocalization();return createText((id,fallback)=>translate(id,fallback));}
