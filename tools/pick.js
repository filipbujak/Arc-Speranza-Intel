const gi=require('@iconify-json/game-icons/icons.json'); const mdi=require('@iconify-json/mdi/icons.json');
const want={
 'quests':['scroll-quill','scroll-unfurled'],
 'locations/antenna':['radar-dish'],'locations/breach_room':['breaking-chain','smash-arrows'],'locations/button':['push','button-finger'],
 'locations/emergency_shelter':['camping-tent'],'locations/extraction':['exit-door','entry-door'],'locations/field_crate':['wooden-crate'],
 'locations/field_depot':['cargo-crate'],'locations/fuel-cell':['battery-pack','batteries','power-generator'],'locations/gondola_landing':['cable-car','ropeway','gondola'],
 'locations/hatch':['floor-hatch','airtight-hatch'],'locations/locked_room':['padlock','locked-door'],'locations/mast_lift':['elevator','lift'],
 'locations/metro_entrance':['tunnel','stairs'],'locations/metro_station':['train','steam-locomotive'],'locations/mountaineers_detector':['metal-detector','radar-sweep'],
 'locations/player_spawn':['person','walking-boot'],'locations/raider_camp':['campfire','camp-fire'],'locations/supply_station':['parachute'],'locations/zipline':['zipline','rope-coil'],
 'containers/ammo_crate':['ammo-box','bullets'],'containers/android':['robot-golem','android-mask'],'containers/arc_courier':['delivery-drone','drone'],
 'containers/arc_probe':['satellite','space-suit'],'containers/husk':['broken-bone','robot-grab','gear-hammer'],'containers/bag':['knapsack','swap-bag'],
 'containers/base_container':['open-chest','chest'],'containers/basket':['basket','wicker-basket','shopping-basket'],'containers/box':['cardboard-box','cardboard-box-closed'],
 'containers/breachable_container':['crowbar'],'containers/car':['city-car','car-door'],'containers/combat_supplies':['crossed-swords'],
 'containers/electrical_container':['power-generator','electrical-socket'],'containers/frozen_raider_cache':['ice-cube','frozen-block'],'containers/hurricane_cache':['tornado','whirlwind'],
 'containers/industrial_container':['factory','oil-drum'],'containers/locker':['locker','closed-doors','wardrobe'],'containers/med_crate':['medical-pack','first-aid-kit'],
 'containers/raider_cache':['locked-chest'],'containers/security_breach':['security-gate','cyber-eye'],'containers/utility_crate':['toolbox'],'containers/weapon_case':['machine-gun','ak47','pistol-gun'],
 'arc':['robot-golem'],'arc/tick':['spider-bot','spider-alt'],'arc/pop':['mine-explosion','unlit-bomb'],'arc/fireball':['fireball','burning-meteor'],'arc/firefly':['dragonfly','fly'],
 'arc/hornet':['hornet','wasp-sting','bee'],'arc/wasp':['wasp-sting'],'arc/turret':['turret','sentry-gun'],'arc/rocketeer':['rocket'],'arc/sentinel':['sentry-gun','laser-turret'],
 'arc/snitch':['spy','eye-target','evil-eyes'],'arc/comet':['comet-spark','meteor-impact'],'arc/rollbot':['wheel','rolling-bomb'],'arc/shredder':['buzzsaw','circular-saw'],
 'arc/bison':['bull-horns','bull'],'arc/bastion':['tank','battle-tank'],'arc/bombardier':['bombing-run','falling-bomb','bomber'],'arc/turbine':['turbine','fan','windmill'],
 'arc/vaporizer':['vapor','smoke-bomb','steam'],'arc/queen':['queen-crown'],'arc/matriarch':['crown','crowned-skull'],'arc/hydra':['hydra'],'arc/bully':['brute','minotaur'],'arc/skulker':['ninja-head','hood'],
 'events':['lightning-trio','sparkles'],'events/antenna':['radar-dish'],'events/assessor':['magnifying-glass'],'events/bird':['raven','dove','bird-twitter'],'events/bunker':['bunker'],
 'events/calibrated_transmitter':['radio-tower','transmitter'],'events/car_alarm':['siren','alarm-clock'],'events/carrier_hatch':['airtight-hatch','floor-hatch'],'events/dig_spot':['shovel','spade'],
 'events/drop_in_loot':['parachute'],'events/emperor_payload':['crown-coin','treasure-map'],'events/flash_freeze':['snowflake-1','snowflake-2'],'events/frigate_cargo':['cargo-ship'],
 'events/harvester':['wheat','harvest'],'events/locked_gate_key':['key','door-key'],'events/mine':['land-mine'],'events/piano':['grand-piano','piano'],'events/puzzle_button':['puzzle'],
 'events/security_camera':['cctv-camera','spy'],'events/ship_model':['sailboat','galleon'],'events/snow_pile':['snowing','snowman'],
 'nature':['oak-leaf','curled-leaf'],'nature/agave':['aloe-vera','sprout'],'nature/apricot':['peach','apple-core','shiny-apple'],'nature/candleberries':['berries-bowl','grapes'],'nature/fertilizer':['powder','fertilizer-bag'],
 'nature/great-mullein':['flowers','flower-emblem'],'nature/lemons':['lemon','orange-slice'],'nature/moss':['moss','vine-leaf'],'nature/mushroom':['mushroom','mushrooms'],'nature/olive':['olive'],
 'nature/prickly-pear':['cactus'],'nature/roots':['root-tip','roots'],'pin':['pin','pushpin'],
};
const out={}, miss=[];
for(const [k,list] of Object.entries(want)){ const n=list.find(x=>gi.icons[x]); if(!n){miss.push(k);continue;}
 const body=gi.icons[n].body; const ds=[...body.matchAll(/ d="([^"]+)"/g)].map(m=>m[1]); if(!ds.length||/<(circle|rect|ellipse|polygon)/.test(body)) {miss.push(k+':'+n+':shape');continue;} out[k]=ds.join(' '); }
console.log('missing',miss, 'w/h', gi.width, gi.height);
const ui={map:'map-outline',quests:'script-text-outline',items:'package-variant-closed',shops:'storefront-outline',settings:'cog',search:'magnify',star:'star',starOutline:'star-outline',chevronDown:'chevron-down',chevronUp:'chevron-up',chevronRight:'chevron-right',eye:'eye',eyeOff:'eye-off',plus:'plus',minus:'minus',check:'check',close:'close',pin:'map-marker',crosshair:'crosshairs-gps',target:'target',layers:'layers-outline',fit:'fit-to-screen-outline',reset:'restore',refresh:'refresh',keyboard:'keyboard-outline',trash:'trash-can-outline',edit:'pencil-outline',goto:'arrow-right-bold-box-outline',lock:'lock-outline',info:'information-outline',coin:'currency-usd',bag:'bag-personal-outline'};
const uo={}, um=[]; for(const [k,n] of Object.entries(ui)){ const i=mdi.icons[n]; if(!i){um.push(n);continue;} const ds=[...i.body.matchAll(/ d="([^"]+)"/g)].map(m=>m[1]); uo[k]=ds.join(' '); }
console.log('ui missing',um,'mdi',mdi.width);
require('fs').writeFileSync('icons.json',JSON.stringify({markers:out,ui:uo}));
console.log(Object.keys(out).length, Object.keys(uo).length);
