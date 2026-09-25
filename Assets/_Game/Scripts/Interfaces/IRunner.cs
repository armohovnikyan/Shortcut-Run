using System;
using UnityEngine;


public interface IRunner
{
  void IsFailing();
  void CheckBoards(); 
  void Jump();
  void ChangeSpeedBonus(float Bonus);
}
