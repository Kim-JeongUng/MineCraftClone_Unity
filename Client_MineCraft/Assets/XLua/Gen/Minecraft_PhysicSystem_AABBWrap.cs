#if USE_UNI_LUA
using LuaAPI = UniLua.Lua;
using RealStatePtr = UniLua.ILuaState;
using LuaCSFunction = UniLua.CSharpFunctionDelegate;
#else
using LuaAPI = XLua.LuaDLL.Lua;
using RealStatePtr = System.IntPtr;
using LuaCSFunction = XLua.LuaDLL.lua_CSFunction;
#endif

using XLua;
using System.Collections.Generic;


namespace XLua.CSObjectWrap
{
    using Utils = XLua.Utils;
    public class MinecraftPhysicSystemAABBWrap 
    {
        public static void __Register(RealStatePtr L)
        {
			ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
			System.Type type = typeof(Minecraft.PhysicSystem.AABB);
			Utils.BeginObjectRegister(type, L, translator, 4, 3, 4, 2);
			Utils.RegisterFunc(L, Utils.OBJ_META_IDX, "__add", __AddMeta);
            Utils.RegisterFunc(L, Utils.OBJ_META_IDX, "__sub", __SubMeta);
            Utils.RegisterFunc(L, Utils.OBJ_META_IDX, "__mul", __MulMeta);
            Utils.RegisterFunc(L, Utils.OBJ_META_IDX, "__eq", __EqMeta);
            
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "Equals", _m_Equals);
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "GetHashCode", _m_GetHashCode);
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "Intersects", _m_Intersects);
			
			
			Utils.RegisterFunc(L, Utils.GETTER_IDX, "Min", _g_get_Min);
            Utils.RegisterFunc(L, Utils.GETTER_IDX, "Max", _g_get_Max);
            Utils.RegisterFunc(L, Utils.GETTER_IDX, "Center", _g_get_Center);
            Utils.RegisterFunc(L, Utils.GETTER_IDX, "Size", _g_get_Size);
            
			Utils.RegisterFunc(L, Utils.SETTER_IDX, "Min", _s_set_Min);
            Utils.RegisterFunc(L, Utils.SETTER_IDX, "Max", _s_set_Max);
            
			
			Utils.EndObjectRegister(type, L, translator, null, null,
			    null, null, null);

		    Utils.BeginClassRegister(type, L, __CreateInstance, 4, 0, 0);
			Utils.RegisterFunc(L, Utils.CLS_IDX, "Merge", _m_Merge_xlua_st_);
            Utils.RegisterFunc(L, Utils.CLS_IDX, "Translate", _m_Translate_xlua_st_);
            Utils.RegisterFunc(L, Utils.CLS_IDX, "Rotate", _m_Rotate_xlua_st_);
            
			
            
			
			
			
			Utils.EndClassRegister(type, L, translator);
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int __CreateInstance(RealStatePtr L)
        {
            
			try {
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
				if(LuaAPI.lua_gettop(L) == 3 && translator.Assignable<UnityEngine.Vector3>(L, 2) && translator.Assignable<UnityEngine.Vector3>(L, 3))
				{
					UnityEngine.Vector3 _min;translator.Get(L, 2, out _min);
					UnityEngine.Vector3 _max;translator.Get(L, 3, out _max);
					
					var gen_ret = new Minecraft.PhysicSystem.AABB(_min, _max);
					translator.PushMinecraftPhysicSystemAABB(L, gen_ret);
                    
					return 1;
				}
				
				if (LuaAPI.lua_gettop(L) == 1)
				{
				    translator.PushMinecraftPhysicSystemAABB(L, default(Minecraft.PhysicSystem.AABB));
			        return 1;
				}
				
			}
			catch(System.Exception gen_e) {
				return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
			}
            return LuaAPI.luaL_error(L, "invalid arguments to Minecraft.PhysicSystem.AABB constructor!");
            
        }
        
		
        
		
        
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int __AddMeta(RealStatePtr L)
        {
            
			try {
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
			
				if (translator.Assignable<Minecraft.PhysicSystem.AABB>(L, 1) && translator.Assignable<Minecraft.PhysicSystem.AABB>(L, 2))
				{
					Minecraft.PhysicSystem.AABB leftside;translator.Get(L, 1, out leftside);
					Minecraft.PhysicSystem.AABB rightside;translator.Get(L, 2, out rightside);
					
					translator.PushMinecraftPhysicSystemAABB(L, leftside + rightside);
					
					return 1;
				}
            
			
				if (translator.Assignable<Minecraft.PhysicSystem.AABB>(L, 1) && translator.Assignable<UnityEngine.Vector3>(L, 2))
				{
					Minecraft.PhysicSystem.AABB leftside;translator.Get(L, 1, out leftside);
					UnityEngine.Vector3 rightside;translator.Get(L, 2, out rightside);
					
					translator.PushMinecraftPhysicSystemAABB(L, leftside + rightside);
					
					return 1;
				}
            
			}
			catch(System.Exception gen_e) {
				return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
			}
            return LuaAPI.luaL_error(L, "invalid arguments to right hand of + operator, need Minecraft.PhysicSystem.AABB!");
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int __SubMeta(RealStatePtr L)
        {
            
			try {
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
			
				if (translator.Assignable<Minecraft.PhysicSystem.AABB>(L, 1) && translator.Assignable<UnityEngine.Vector3>(L, 2))
				{
					Minecraft.PhysicSystem.AABB leftside;translator.Get(L, 1, out leftside);
					UnityEngine.Vector3 rightside;translator.Get(L, 2, out rightside);
					
					translator.PushMinecraftPhysicSystemAABB(L, leftside - rightside);
					
					return 1;
				}
            
			}
			catch(System.Exception gen_e) {
				return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
			}
            return LuaAPI.luaL_error(L, "invalid arguments to right hand of - operator, need Minecraft.PhysicSystem.AABB!");
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int __MulMeta(RealStatePtr L)
        {
            
			try {
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
			
				if (translator.Assignable<Minecraft.PhysicSystem.AABB>(L, 1) && translator.Assignable<UnityEngine.Quaternion>(L, 2))
				{
					Minecraft.PhysicSystem.AABB leftside;translator.Get(L, 1, out leftside);
					UnityEngine.Quaternion rightside;translator.Get(L, 2, out rightside);
					
					translator.PushMinecraftPhysicSystemAABB(L, leftside * rightside);
					
					return 1;
				}
            
			}
			catch(System.Exception gen_e) {
				return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
			}
            return LuaAPI.luaL_error(L, "invalid arguments to right hand of * operator, need Minecraft.PhysicSystem.AABB!");
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int __EqMeta(RealStatePtr L)
        {
            
			try {
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
			
				if (translator.Assignable<Minecraft.PhysicSystem.AABB>(L, 1) && translator.Assignable<Minecraft.PhysicSystem.AABB>(L, 2))
				{
					Minecraft.PhysicSystem.AABB leftside;translator.Get(L, 1, out leftside);
					Minecraft.PhysicSystem.AABB rightside;translator.Get(L, 2, out rightside);
					
					LuaAPI.lua_pushboolean(L, leftside == rightside);
					
					return 1;
				}
            
			}
			catch(System.Exception gen_e) {
				return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
			}
            return LuaAPI.luaL_error(L, "invalid arguments to right hand of == operator, need Minecraft.PhysicSystem.AABB!");
            
        }
        
        
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_Equals(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
                Minecraft.PhysicSystem.AABB gen_to_be_invoked;translator.Get(L, 1, out gen_to_be_invoked);
            
            
                
                {
                    object _obj = translator.GetObject(L, 2, typeof(object));
                    
                        var gen_ret = gen_to_be_invoked.Equals( _obj );
                        LuaAPI.lua_pushboolean(L, gen_ret);
                    
                    
                        translator.UpdateMinecraftPhysicSystemAABB(L, 1, gen_to_be_invoked);
                    
                    
                    return 1;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_GetHashCode(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
                Minecraft.PhysicSystem.AABB gen_to_be_invoked;translator.Get(L, 1, out gen_to_be_invoked);
            
            
                
                {
                    
                        var gen_ret = gen_to_be_invoked.GetHashCode(  );
                        LuaAPI.xlua_pushinteger(L, gen_ret);
                    
                    
                        translator.UpdateMinecraftPhysicSystemAABB(L, 1, gen_to_be_invoked);
                    
                    
                    return 1;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_Intersects(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
                Minecraft.PhysicSystem.AABB gen_to_be_invoked;translator.Get(L, 1, out gen_to_be_invoked);
            
            
                
                {
                    Minecraft.PhysicSystem.AABB _aabb;translator.Get(L, 2, out _aabb);
                    
                        var gen_ret = gen_to_be_invoked.Intersects( _aabb );
                        LuaAPI.lua_pushboolean(L, gen_ret);
                    
                    
                        translator.UpdateMinecraftPhysicSystemAABB(L, 1, gen_to_be_invoked);
                    
                    
                    return 1;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_Merge_xlua_st_(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
            
                
                {
                    Minecraft.PhysicSystem.AABB _left;translator.Get(L, 1, out _left);
                    Minecraft.PhysicSystem.AABB _right;translator.Get(L, 2, out _right);
                    
                        var gen_ret = Minecraft.PhysicSystem.AABB.Merge( _left, _right );
                        translator.PushMinecraftPhysicSystemAABB(L, gen_ret);
                    
                    
                    
                    return 1;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_Translate_xlua_st_(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
            
                
                {
                    Minecraft.PhysicSystem.AABB _aabb;translator.Get(L, 1, out _aabb);
                    UnityEngine.Vector3 _translation;translator.Get(L, 2, out _translation);
                    
                        var gen_ret = Minecraft.PhysicSystem.AABB.Translate( _aabb, _translation );
                        translator.PushMinecraftPhysicSystemAABB(L, gen_ret);
                    
                    
                    
                    return 1;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_Rotate_xlua_st_(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
            
			    int gen_param_count = LuaAPI.lua_gettop(L);
            
                if(gen_param_count == 2&& translator.Assignable<Minecraft.PhysicSystem.AABB>(L, 1)&& translator.Assignable<UnityEngine.Quaternion>(L, 2)) 
                {
                    Minecraft.PhysicSystem.AABB _aabb;translator.Get(L, 1, out _aabb);
                    UnityEngine.Quaternion _rotation;translator.Get(L, 2, out _rotation);
                    
                        var gen_ret = Minecraft.PhysicSystem.AABB.Rotate( _aabb, _rotation );
                        translator.PushMinecraftPhysicSystemAABB(L, gen_ret);
                    
                    
                    
                    return 1;
                }
                if(gen_param_count == 3&& translator.Assignable<Minecraft.PhysicSystem.AABB>(L, 1)&& translator.Assignable<UnityEngine.Quaternion>(L, 2)&& translator.Assignable<UnityEngine.Vector3>(L, 3)) 
                {
                    Minecraft.PhysicSystem.AABB _aabb;translator.Get(L, 1, out _aabb);
                    UnityEngine.Quaternion _rotation;translator.Get(L, 2, out _rotation);
                    UnityEngine.Vector3 _pivot;translator.Get(L, 3, out _pivot);
                    
                        var gen_ret = Minecraft.PhysicSystem.AABB.Rotate( _aabb, _rotation, _pivot );
                        translator.PushMinecraftPhysicSystemAABB(L, gen_ret);
                    
                    
                    
                    return 1;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
            return LuaAPI.luaL_error(L, "invalid arguments to Minecraft.PhysicSystem.AABB.Rotate!");
            
        }
        
        
        
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _g_get_Min(RealStatePtr L)
        {
		    try {
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
			
                Minecraft.PhysicSystem.AABB gen_to_be_invoked;translator.Get(L, 1, out gen_to_be_invoked);
                translator.PushUnityEngineVector3(L, gen_to_be_invoked.Min);
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            return 1;
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _g_get_Max(RealStatePtr L)
        {
		    try {
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
			
                Minecraft.PhysicSystem.AABB gen_to_be_invoked;translator.Get(L, 1, out gen_to_be_invoked);
                translator.PushUnityEngineVector3(L, gen_to_be_invoked.Max);
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            return 1;
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _g_get_Center(RealStatePtr L)
        {
		    try {
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
			
                Minecraft.PhysicSystem.AABB gen_to_be_invoked;translator.Get(L, 1, out gen_to_be_invoked);
                translator.PushUnityEngineVector3(L, gen_to_be_invoked.Center);
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            return 1;
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _g_get_Size(RealStatePtr L)
        {
		    try {
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
			
                Minecraft.PhysicSystem.AABB gen_to_be_invoked;translator.Get(L, 1, out gen_to_be_invoked);
                translator.PushUnityEngineVector3(L, gen_to_be_invoked.Size);
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            return 1;
        }
        
        
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _s_set_Min(RealStatePtr L)
        {
		    try {
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
			
                Minecraft.PhysicSystem.AABB gen_to_be_invoked;translator.Get(L, 1, out gen_to_be_invoked);
                UnityEngine.Vector3 gen_value;translator.Get(L, 2, out gen_value);
				gen_to_be_invoked.Min = gen_value;
            
                translator.UpdateMinecraftPhysicSystemAABB(L, 1, gen_to_be_invoked);
            
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            return 0;
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _s_set_Max(RealStatePtr L)
        {
		    try {
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
			
                Minecraft.PhysicSystem.AABB gen_to_be_invoked;translator.Get(L, 1, out gen_to_be_invoked);
                UnityEngine.Vector3 gen_value;translator.Get(L, 2, out gen_value);
				gen_to_be_invoked.Max = gen_value;
            
                translator.UpdateMinecraftPhysicSystemAABB(L, 1, gen_to_be_invoked);
            
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            return 0;
        }
        
		
		
		
		
    }
}
